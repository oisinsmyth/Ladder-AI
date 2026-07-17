# review-conventions skill — blind validation against test-project001 + reference corpora (2026-07-16)

**What this is.** Step 3 of `docs/15-generation-pipeline.md`'s build order: the
`.claude/skills/review-conventions/SKILL.md` reviewer (pipeline skill #9, the "Check" phase's
conventions stage — mechanical `converter review` wrapped by an AI pass), validated the way
docs/15 defines reviewers to run — a **fresh-context subagent** given only the skill file, the
binding docs (CLAUDE.md, `docs/06-lad-conventions.md`, the stage-gates "S4 Phase 1" pointer,
`ir/SPEC.md`, `patterns/`), and both corpora (`ir/test-project001/`, all 21 files;
`ir/reference/`, all 14 files), explicitly barred from `docs/notes/test-project001-retrospective.md`,
the prior validation note (`docs/notes/review-simplicity-validation-2026-07-16.md`, and any
`docs/notes/*validation*.md`), git history, and this note's own expected-findings anchor. The
agent had no authoring context and no conversation history about this code. Its full report is
preserved verbatim in §3; §1 is the comparison against the expected findings, §2 the verdict.

Corpus state at validation: last `ir/` commit `19b2022` ("Fix In_Cycle lamp and DI4 stop
polarity") — the two owner-ruled tier-1 bugs fixed, the C-308 scan-copy trap still present. The
tool binary was built fresh in this worktree (Release, net8.0) from the current HEAD.

## 1. Comparison against expected findings

### Drift check (hard pass/fail) — PASSED, byte-identical

The executor's own two baseline runs, done before the blind run:

```
$ ./src/converter/Converter/bin/Release/net8.0/converter.exe review ir/test-project001/*.ir --ignore-errors
…
SUMMARY: 21 file(s), 17 finding(s) (16 error, 1 warn)

$ ./src/converter/Converter/bin/Release/net8.0/converter.exe review ir/reference/*.ir --ignore-errors
…
SUMMARY: 14 file(s), 29 finding(s) (14 error, 15 warn)
```

Both match the pinned baselines exactly (the reference figure is also the S4 pilot record's
29/14/15). test-project001 breakdown confirmed: C-201 header errors ×11, C-201 network-5 title error,
C-301 error N15 + C-501 warn N15, C-406 error N13 + C-406 declaration errors ×2 (FB Static + iDB).

The blind run invoked the tool itself (one 35-file command, `SUMMARY: 35 file(s), 46 finding(s)
(30 error, 16 warn)`, then — after display truncation of the single run — four batch re-runs
captured verbatim; batch totals reconcile: 6+11+13+16 findings, 6+10+7+7 error, 0+1+6+9 warn).
Mechanical comparison, blind report's embedded output vs the executor's baselines, normalized
per-file and sorted: **all 46 findings identical** (severity, rule ID, location, description) and
**all 315 per-rule status lines identical**. No re-derivation, paraphrase, or re-severitying
anywhere — the anti-drift contract held.

### Expected AI findings (test-project001)

| Expected (must-find) | Blind run result |
|---|---|
| C-308 error, FC_ControlMain N5: eight cyclic scan-copies `DB_Settings.Pusher*/Pressure*` → `iDB_PusherControl.IO.*` (lines 57–64) | **Found**, exact lines quoted — plus the ninth writer (`COIL …IO.Fitted := DB_Settings.PusherFitted`, line 46) folded into the same finding |
| C-307 warn + C-122 error: the sequencer's step timings homed in `DB_Settings`, PTs not from its own interface UDT | **Found** — C-122 error with the full PT chain quoted + C-307 settings-home table row; extended by a second C-122 on the pusher (its PT chain *starts* at DB_Settings via the scan-copies — same root cause traced whole-project) and a genuinely new C-122 on step 40's missing dwell timer (verified: network 10 is three MOVEs, no TON) |
| C-403 error, project scope: S/R-written bits with no OB100-class reset block anywhere; regime split correct | **Found** — complete S/R inventory from the imported FB kept as *context*, the missing reset block filed against the *generated integration*, exactly the required split |
| C-124 error: RETAIN `Step`/run-state never force-reset at restart | **Found** — with the fault-latch carve-out explicitly honored (latches deliberately not flagged) |
| C-305 warn + C-111 error: no `DB_PLC`/simulation machinery — flagged with the owner question form | **Found** — both filed, `Test[0]` correctly distinguished from C-111 simulation, owner "reaffirm or schedule" item carried |
| C-502 warn: `FC_GeneralAlarms`/`FC_EStopAlarms` absent — owner scale-down question, not adjudicated | **Found**, exact form — plus the sharp observation that this IO set has no E-Stop input to monitor |
| C-001 error: `Snake_Case` buffer members in DB_Input/DB_Output | **Found** — all 26 members enumerated, correctly not double-cited against the tool's C-003 |
| Hand-checked: `MotorFwdRevIOSet` missing `UDT_` prefix (imported-real context) | **Found** — labeled "hand-checked — tool reports NotApplicable for this content kind", context not demand |
| Hand-checked tag-table sweep with the `Clock_0.5Hz` tension | **Found** — tension-for-owner form (rename vs tolerate the vendor default), never a fix demand; plus a grounded C-001 hand-check on the 52 dead legacy `Tag_*` entries |

| Expected (should-find, context) | Blind run result |
|---|---|
| C-402: `HandPosEdge` written twice, `HandNegEdge` declared and never referenced (imported-real) | **Found** — exact evidence, context regime, plus the `NOT HandPosEdge AND NOT HandPosEdge` duplicate |
| C-407 clean/n-a declaration for test-project001 | **Found** — clean (all FB timers multi-instance, no standalone timers, no DB_Timers needed), plus a context C-407 finding on the *reference* corpus's `FBTimers` second home |

Expected-clean rules — C-127, C-109/C-110, C-304, C-408, C-105, C-306, C-118/119/120/121,
C-501-residual packing — **all declared clean with enumerated evidence** (e.g. all 18 sequencer
and 8 pusher `=> IO.Step` writes checked for C-121 shape; FC_ControlMain's iDB references
correctly recognized as the orchestrator's *intended* role, not a C-127 hit).

### Negative controls — all held

| Must NOT appear | Result |
|---|---|
| The two fixed tier-1 bugs (In_Cycle lamp dead; DI4 NC polarity) re-raised as findings | **Absent.** The run verified the fixed states as clean — DI4's NC polarity "absorbed at the map with a comment stating exactly that (the pattern's negated variant)"; In_Cycle driven from field feedbacks — and noted only the accurate post-fix residue (the now-dead `IO.InCycle` member, whose removal FC_ControlMain's own comment defers). Executor re-verified both in the IR. |
| Any C-6xx/C-101/C-126/C-203 citation in this skill's findings sections | **Absent.** Those IDs appear only as routing lines inside "Belongs to review-simplicity" (plus one explanatory C-126 mention inside a clean declaration) — the citation split held. |
| C-309 clamp findings, C-121-verbosity complaints, mapping-rail/Test-array flags | **Absent.** C-309 anti-finding explicitly declared honored; rail and Test arrays calibrated out with the pattern named. |
| Hand-duplicates of mechanical findings | **Absent.** Where the agent disagreed with the tool (C-003's `iDB_<FBName>_<Instance>` sub-clause unenforced — both generated iDBs lack an `_<Instance>` segment yet pass) it filed a **converter gap report** instead of a duplicate finding — exactly the designed tool-disagreement path. |

### Reference-corpus disposition (CHECK 3)

Context regime applied to all 14 files ✓. The packed-alarm-word question
(`NodeStatusAlarms`/`PerimeterSafetyAlarms`) surfaced as the standing owner-ruling item, not
adjudicated ✓. `DataHandling.ir`'s C-105 fencing checked on content (name + header comment) and
the tool's C-301 exemption confirmed appropriate ✓. Reference-scope limits declared explicitly
(no OB1/alarm architecture to check Group 2 rules against — n/a, not silently passed) ✓.

### Format & honesty (CHECK 4)

Template followed section-for-section ✓; every block regime-labeled ✓; bucket labels on every AI
finding, bucket-C findings say "judgment" ✓; all six Group 4 standing declines present with
reasons ✓; blindness declared prominently (see §2) ✓; tool binary + all five invocations quoted,
single-command discipline followed ✓.

### New genuine discoveries beyond the anchor (executor spot-verified each against the IR)

1. **C-103 cross-block writer conflict on `IO.FaultFB`** — FC_ControlMain plain-COILs it from the
   field buffer every scan (line 34) while the FB `RCOIL`s the same bit on FaultReset (line 184),
   no SCOIL anywhere. Verified.
2. **C-122: step 40 has no dwell timer of its own** (network 10 = three MOVE transitions, no
   TON) — dwells indefinitely if forward feedback never arrives and the motor FB doesn't fault.
   Verified.
3. **Tier-1: motor timing settings unconfigured** — the imported FB's iDB carries only
   `ReverseDelay = 8.0`; `FTTime`/`EnableUPSTime`/`ShutdownTime`/`ReverseIgnoreFT` have no start
   values, so `FaultTripTimer` PT = 0 and FTR would latch effectively immediately on a real start.
   Verified (single `= 8.0` start value in the iDB).
4. **Candidate rule gap: one-writer discipline for non-settings interface state bits** —
   `RecentStart := AlwaysTrue` every scan (line 30) fights the FB's own self-clearing management;
   C-308 is settings-only, C-402 edge-only, so no current rule names it. Verified.
5. **Converter backlog item:** C-003's instance-DB sub-clause (`iDB_<FBName>_<Instance>`)
   unenforced (see negative-controls row).
6. **C-507×C-123 rules-interaction:** the reset-required latched fault alarms are de facto
   acknowledge-required while C-507's default is self-clearing — candidates for the per-alarm
   exception list, filed as such.
7. **C-503 dual-home question:** FTR/FTS live in both the motor's per-instance alarm word and the
   category word — which does the HMI bind?
8. **`DB_Settings` end-state question:** after the queued C-307/C-308 rework every current member
   moves out — owner to confirm an empty `DB_Settings` is intended.

Missed vs the anchor: nothing. Every expected item was found in the expected form.

## 2. Verdict

**Validated.** The drift check — the hard gate — passed byte-identically (46/46 findings, 315/315
status lines). Every must-find and should-find item reproduced independently with correct
severities, buckets, and regime labels; all four negative controls held; calibration rules were
applied correctly throughout (anti-findings honored, pattern-sanctioned shapes skipped, tensions
flagged for owner ruling rather than adjudicated); and the run extended the anchor with grounded
new findings, each spot-verified by the examiner against the IR.

**Blindness outcome, stated honestly.** The blind agent itself declared **"informed, not blind"**:
it needed regime labels (generated vs imported-real), the skill points at stage-gates for those,
and the surrounding stage-gates S6 entries summarize prior reviews of this same corpus. It
declared exactly what it read, re-derived every finding from the IR with quoted evidence, and
recommended that gate-grade runs fence stage-gates to the S4 section and supply regime labels
externally — a recommendation this note adopts for future runs of either review skill. And the
sibling note's method caveat applies here identically: the skill, the expected-findings anchor,
and this comparison were produced by the same session — **"blind executor, non-blind examiner."**
The S4-precedent stronger form (owner independently reviews the same corpus and compares) remains
available if wanted before leaning on this skill for gate decisions.

**Bash scoping record** (owed by the plan). The skill's `allowed-tools` carries both scoped
spellings — `Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe review:*)` and the
non-`./` variant — plus the body-level "Bash exists in this skill for exactly this one command"
restriction as belt-and-braces. Whether the frontmatter scoping *binds* was not exercisable by
this validation: the blind agent executes the skill from its file content in an Agent-tool
context, not as a slash-command invocation, so no permission-rule matching occurred. If live
`/review-conventions` use shows the scoped rules failing to match (e.g. an invocation prefixed
with `cd … &&` falls outside the prefix), the documented fallback is plain `Bash` in
`allowed-tools` with the body-level restriction carrying the constraint. The body's
"single command, no chaining, no pipes" instruction exists to keep real invocations inside the
scoped prefix — the blind run followed it (five single-command invocations, quoted in its
report header).

**Cost note:** the blind run took ~37 minutes, ~318k tokens, 80 tool calls for the 35-file,
two-corpus scope (mechanical pass + all four AI-pass groups) — gate-review-sized, consistent with
the skill's own cost section (the sibling measured ~20 min/~200k for 21 files and fewer rules).
Scope per-block per the skill's inner-loop guidance for anything smaller than a gate.

**Owner follow-ups surfaced by this run:** the queued settings rework now carries a concrete
end-state question (`DB_Settings` would be left empty — confirm or name what's genuinely
plant-wide); tier-1 candidates worth early verification — `RecentStart` constant-forcing,
`FaultFB`'s intended reset path, the motor iDB's missing timing start values (FTR at PT=0);
rulings queued — C-123's letter vs `ParkedTimeoutFault`'s documented alarm-only design, C-502
scale-down, the C-507 exception list, C-115 vocabulary scope beyond the motor family, C-201's
applicability to UDTs, and the standing packed-alarm-word question; converter backlog — the
C-003 `iDB_<FBName>_<Instance>` sub-clause.

## 3. Blind run report (verbatim)

The full report as returned by the fresh-context agent, unedited:

---

# Conventions review — ir/test-project001/ (21 files) + ir/reference/ (14 files) (2026-07-16)

Blindness: **informed, not blind — declared in full.** This is a fresh-context run: none of the reviewed code was authored in this session, no author reasoning is in context, and the fenced files were not opened (docs/notes/test-project001-retrospective.md, docs/notes/review-simplicity-validation-2026-07-16.md, no `*validation*.md`, no gen/, no git history). Per the skill's inputs I read this run: `docs/06-lad-conventions.md` in full (all six convention sections + preamble + Simplicity section — its rule rationales cite corpus examples, including C-308's naming of the test-project001 `FC_ControlMain` trap and C-126/C-127's origin notes); `docs/notes/stage-gates.md` — the "S4 Phase 1" section (checkability grounding, CheckedVacuous labels, severity-vs-checkability mismatches) **plus** the surrounding S3→S6 entries (~lines 3540–4040), which I needed for regime labels. Those S6 entries summarize prior reviews of this same corpus (retrospective distillations, the simplicity-validation highlights, owner rulings), so on the "another review's report of the same content" axis this run is informed, not blind. Every AI-pass finding below is re-derived from the IR read this run, with its own quoted evidence (grep/read), never from a rationale's or summary's claim. `converter review` was run as the mechanical half by design — not contamination. Also read as sanctioned inputs: `ir/SPEC.md` (file shape, statement-kind ordering, DB/TYPE/TAGTABLE grammar), `patterns/README.md`, and `patterns/{motor-dol,chained-permissive-enable,input-mapping,db-inputs}/pattern.md`. **If this report feeds a gate decision, recommend a rerun with stage-gates access fenced to the S4 Phase 1 section only and regime labels supplied externally.** No F-/safety block was encountered anywhere in scope (`PerimeterSafetyAlarms`/`EquipmentStatus` monitor safety *status tags* through the standard program; no F-internals present) — hard rule 2 not triggered.

Tool: `./src/converter/Converter/bin/Release/net8.0/converter.exe` — invocation: `./src/converter/Converter/bin/Release/net8.0/converter.exe review ir/test-project001/*.ir ir/reference/*.ir --ignore-errors` (single command over all 35 files, from the repo root; exit code 1 = findings present). Its full-scope output exceeded the shell harness's 30,000-character display limit and was truncated mid-stream in display only, so the identical tool was re-run in four batches (each a single command, no chaining, no pipes) to capture every line verbatim:
1. `./src/converter/Converter/bin/Release/net8.0/converter.exe review ir/test-project001/DB_Alarms.ir ir/test-project001/DB_AnalogInput.ir ir/test-project001/DB_Controls.ir ir/test-project001/DB_Input.ir ir/test-project001/DB_Output.ir ir/test-project001/DB_Settings.ir ir/test-project001/DefaultTagTable.ir ir/test-project001/MotorFwdRevIOSet.ir ir/test-project001/UDT_PusherIO.ir ir/test-project001/UDT_ShredderSequencerIO.ir ir/test-project001/iDB_MotorFwdRevSystem_Shredder.ir ir/test-project001/iDB_PusherControl.ir ir/test-project001/iDB_ShredderSequencer.ir --ignore-errors`
2. `./src/converter/Converter/bin/Release/net8.0/converter.exe review ir/test-project001/FB_MotorFwdRevSystem.ir ir/test-project001/FB_PusherControl.ir ir/test-project001/FB_ShredderSequencer.ir ir/test-project001/FC_AlarmsMain.ir ir/test-project001/FC_ControlMain.ir ir/test-project001/FC_Inputs.ir ir/test-project001/FC_Outputs.ir ir/test-project001/Main.ir --ignore-errors`
3. `./src/converter/Converter/bin/Release/net8.0/converter.exe review ir/reference/AlarmWords.ir ir/reference/BooleanExtras.ir ir/reference/CommsProcessData.ir ir/reference/DataHandling.ir ir/reference/DB_Timers.ir ir/reference/EquipmentStatus.ir ir/reference/FBTimers.ir --ignore-errors`
4. `./src/converter/Converter/bin/Release/net8.0/converter.exe review ir/reference/NodeStatusAlarms.ir ir/reference/PerimeterSafetyAlarms.ir ir/reference/ScaleValue.ir ir/reference/SignalConditioning.ir ir/reference/ThresholdAlarms.ir ir/reference/TimerSample.ir ir/reference/TimingAndCalls.ir --ignore-errors`

The full-scope run's own SUMMARY line, captured before truncation: `SUMMARY: 35 file(s), 46 finding(s) (30 error, 16 warn)`. Batch totals reconcile exactly: 6+11+13+16 = 46 findings; 6+10+7+7 = 30 error; 0+1+6+9 = 16 warn.

Blocks reviewed (regime source: stage-gates S6 entries, declared above):
- ir/test-project001: `Main` (OB1), `FC_Inputs`, `FC_Outputs`, `FC_ControlMain`, `FC_AlarmsMain`, `FB_PusherControl`, `FB_ShredderSequencer`, `UDT_PusherIO`, `UDT_ShredderSequencerIO`, `iDB_PusherControl`, `iDB_ShredderSequencer`, `iDB_MotorFwdRevSystem_Shredder` (scaffolding authored for this project), `DB_Input`, `DB_Output`, `DB_Settings`, `DB_Controls`, `DB_Alarms`, `DB_AnalogInput`, `DefaultTagTable` (project tag surface; contains pre-existing sandbox leftovers) — all **generated**. `FB_MotorFwdRevSystem`, `MotorFwdRevIOSet` — **imported-real** (imported unmodified).
- ir/reference (all 14): `AlarmWords`, `BooleanExtras`, `CommsProcessData`, `DataHandling`, `DB_Timers`, `EquipmentStatus`, `FBTimers`, `NodeStatusAlarms`, `PerimeterSafetyAlarms`, `ScaleValue`, `SignalConditioning`, `ThresholdAlarms`, `TimerSample`, `TimingAndCalls` — **imported-real** (Green-tier reference corpus; `PerimeterSafetyAlarms`' title/comment and `TimerSample`'s title were added by earlier S3 AI work per stage-gates). Imported-real findings below are documented context/calibration, never fix demands.

## Mechanical findings (converter review — verbatim, not re-derived)

**Batch 1 (test-project001 data/type files):**

```
FILE: ir/test-project001/DB_Alarms.ir
  BLOCK: DB_Alarms

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, clean
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)


FILE: ir/test-project001/DB_AnalogInput.ir
  BLOCK: DB_AnalogInput

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, clean
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)


FILE: ir/test-project001/DB_Controls.ir
  BLOCK: DB_Controls

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, clean
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)


FILE: ir/test-project001/DB_Input.ir
  BLOCK: DB_Input

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)

  [Error] C-201 (block level)
    'DB_Input' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/test-project001/DB_Output.ir
  BLOCK: DB_Output

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)

  [Error] C-201 (block level)
    'DB_Output' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/test-project001/DB_Settings.ir
  BLOCK: DB_Settings

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, clean
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)


FILE: ir/test-project001/DefaultTagTable.ir
  BLOCK: 

  C-003: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-005: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-201: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-301: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-501: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-406: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-102: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-401: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-404: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)


FILE: ir/test-project001/MotorFwdRevIOSet.ir
  BLOCK: 

  C-003: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-005: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-201: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-301: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-501: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-406: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-102: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-401: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-404: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)


FILE: ir/test-project001/UDT_PusherIO.ir
  BLOCK: 

  C-003: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-005: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-201: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-301: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-501: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-406: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-102: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-401: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-404: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)


FILE: ir/test-project001/UDT_ShredderSequencerIO.ir
  BLOCK: 

  C-003: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-005: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-201: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-301: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-501: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-406: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-102: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-401: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)
  C-404: not applicable (TYPE/TAGTABLE rule support not implemented in Phase 1)


FILE: ir/test-project001/iDB_MotorFwdRevSystem_Shredder.ir
  BLOCK: iDB_MotorFwdRevSystem_Shredder

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, 1 finding(s)
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)

  [Error] C-201 (block level)
    'iDB_MotorFwdRevSystem_Shredder' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).
  [Error] C-406 (block level)
    Member 'HrTotaliserTimer' is declared as TONR_TIME - only TON_TIME is permitted.
    fix: Replace with a TON_TIME instance plus explicit inversion/edge logic per C-406.

FILE: ir/test-project001/iDB_PusherControl.ir
  BLOCK: iDB_PusherControl

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)

  [Error] C-201 (block level)
    'iDB_PusherControl' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/test-project001/iDB_ShredderSequencer.ir
  BLOCK: iDB_ShredderSequencer

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)

  [Error] C-201 (block level)
    'iDB_ShredderSequencer' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

SUMMARY: 13 file(s), 6 finding(s) (6 error, 0 warn)
```

**Batch 2 (test-project001 code blocks):**

```
FILE: ir/test-project001/FB_MotorFwdRevSystem.ir
  BLOCK: FB_MotorFwdRevSystem

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 2 finding(s)
  C-301: checked, 2 finding(s)
  C-501: checked, 1 finding(s)
  C-406: checked, 2 finding(s)
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Error] C-201 (block level)
    'FB_MotorFwdRevSystem' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).
  [Error] C-201 network 5
    Network 5 has real logic but no title.
    fix: Add a NETWORK title describing what this network does.
  [Error] C-301 network 15
    Network 15 writes 3 slice-access bit(s) (IO.Alarm.%X0, IO.Alarm.%X1, IO.Alarm.%X2) without satisfying the C-501 alarm-word exception (exactly one bit, network titled).
    fix: Split into one network per alarm bit, each titled with its own alarm text, or move this logic into a self-identified data-handling/comms block (C-105).
  [Warn] C-501 network 15
    Network 15's slice-access alarm bit(s) don't satisfy C-501's own conditions (exactly one bit per network, network title states the alarm text).
    fix: Either restructure to satisfy C-501 as written, or - if this packed/summarized form is intentionally fine - propose it as a documented exception in docs/06-lad-conventions.md rather than leaving the rule and the practice disagreeing.
  [Error] C-406 network 13
    Network 13 calls 'HrTotaliserTimer' as a Tonr timer - only TON is permitted.
    fix: Replace with TON plus explicit inversion/edge logic per C-406's own construction guidance (off-delay/retentive behavior built from TON, not TOF/TONR).
  [Error] C-406 (block level)
    Member 'HrTotaliserTimer' is declared as TONR_TIME - only TON_TIME is permitted.
    fix: Replace with a TON_TIME instance plus explicit inversion/edge logic per C-406.

FILE: ir/test-project001/FB_PusherControl.ir
  BLOCK: FB_PusherControl

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, clean
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)


FILE: ir/test-project001/FB_ShredderSequencer.ir
  BLOCK: FB_ShredderSequencer

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, clean
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)


FILE: ir/test-project001/FC_AlarmsMain.ir
  BLOCK: FC_AlarmsMain

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Error] C-201 (block level)
    'FC_AlarmsMain' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/test-project001/FC_ControlMain.ir
  BLOCK: FC_ControlMain

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Error] C-201 (block level)
    'FC_ControlMain' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/test-project001/FC_Inputs.ir
  BLOCK: FC_Inputs

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Error] C-201 (block level)
    'FC_Inputs' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/test-project001/FC_Outputs.ir
  BLOCK: FC_Outputs

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Error] C-201 (block level)
    'FC_Outputs' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/test-project001/Main.ir
  BLOCK: Main

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Error] C-201 (block level)
    'Main' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

SUMMARY: 8 file(s), 11 finding(s) (10 error, 1 warn)
```

**Batch 3 (reference A–F):**

```
FILE: ir/reference/AlarmWords.ir
  BLOCK: AlarmWords

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)

  [Warn] C-003 (block level)
    DB name 'AlarmWords' does not start with the required 'DB_' prefix.
    fix: Rename to 'DB_AlarmWords' (coordinate with any callers/references first).
  [Error] C-201 (block level)
    'AlarmWords' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/reference/BooleanExtras.ir
  BLOCK: BooleanExtras

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, clean
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Warn] C-003 (block level)
    Block name 'BooleanExtras' does not start with the required 'FC_' prefix for a FC.
    fix: Rename to 'FC_BooleanExtras' (coordinate with any callers/references first).

FILE: ir/reference/CommsProcessData.ir
  BLOCK: CommsProcessData

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)

  [Warn] C-003 (block level)
    DB name 'CommsProcessData' does not start with the required 'DB_' prefix.
    fix: Rename to 'DB_CommsProcessData' (coordinate with any callers/references first).
  [Error] C-201 (block level)
    'CommsProcessData' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/reference/DataHandling.ir
  BLOCK: DataHandling

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, clean
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Warn] C-003 (block level)
    Block name 'DataHandling' does not start with the required 'FC_' prefix for a FC.
    fix: Rename to 'FC_DataHandling' (coordinate with any callers/references first).

FILE: ir/reference/DB_Timers.ir
  BLOCK: DB_Timers

  C-003: checked, clean
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)

  [Error] C-201 (block level)
    'DB_Timers' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/reference/EquipmentStatus.ir
  BLOCK: EquipmentStatus

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, clean
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)

  [Warn] C-003 (block level)
    DB name 'EquipmentStatus' does not start with the required 'DB_' prefix.
    fix: Rename to 'DB_EquipmentStatus' (coordinate with any callers/references first).
  [Error] C-201 (block level)
    'EquipmentStatus' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/reference/FBTimers.ir
  BLOCK: FBTimers

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: not applicable (DB-kind file has no networks)
  C-501: not applicable (DB-kind file has no networks)
  C-406: checked, 2 finding(s)
  C-102: not applicable (DB-kind file has no networks/instructions)
  C-401: not applicable (DB-kind file has no networks/instructions)
  C-404: not applicable (DB-kind file has no networks/instructions)

  [Warn] C-003 (block level)
    DB name 'FBTimers' does not start with the required 'DB_' prefix.
    fix: Rename to 'DB_FBTimers' (coordinate with any callers/references first).
  [Error] C-201 (block level)
    'FBTimers' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).
  [Error] C-406 (block level)
    Member 'RunTimeTimer' is declared as TONR_TIME - only TON_TIME is permitted.
    fix: Replace with a TON_TIME instance plus explicit inversion/edge logic per C-406.
  [Error] C-406 (block level)
    Member 'HoldTimer' is declared as TOF_TIME - only TON_TIME is permitted.
    fix: Replace with a TON_TIME instance plus explicit inversion/edge logic per C-406.

SUMMARY: 7 file(s), 13 finding(s) (7 error, 6 warn)
```

**Batch 4 (reference N–T):**

```
FILE: ir/reference/NodeStatusAlarms.ir
  BLOCK: NodeStatusAlarms

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, 2 finding(s)
  C-301: checked, 2 finding(s)
  C-501: checked, 1 finding(s)
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Warn] C-003 (block level)
    Block name 'NodeStatusAlarms' does not start with the required 'FC_' prefix for a FC.
    fix: Rename to 'FC_NodeStatusAlarms' (coordinate with any callers/references first).
  [Error] C-201 (block level)
    'NodeStatusAlarms' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).
  [Error] C-201 network 1
    Network 1 has real logic but no title.
    fix: Add a NETWORK title describing what this network does.
  [Error] C-301 network 1
    Network 1 writes 15 slice-access bit(s) (AlarmWords.AlarmWord0.%X0, AlarmWords.AlarmWord0.%X1, AlarmWords.AlarmWord0.%X2, AlarmWords.AlarmWord0.%X3, AlarmWords.AlarmWord0.%X4, AlarmWords.AlarmWord0.%X5, AlarmWords.AlarmWord0.%X6, AlarmWords.AlarmWord0.%X7, AlarmWords.AlarmWord0.%X8, AlarmWords.AlarmWord0.%X9, AlarmWords.AlarmWord0.%X10, AlarmWords.AlarmWord0.%X11, AlarmWords.AlarmWord0.%X12, AlarmWords.AlarmWord0.%X13, AlarmWords.AlarmWord0.%X14) without satisfying the C-501 alarm-word exception (exactly one bit, network titled).
    fix: Split into one network per alarm bit, each titled with its own alarm text, or move this logic into a self-identified data-handling/comms block (C-105).
  [Warn] C-501 network 1
    Network 1's slice-access alarm bit(s) don't satisfy C-501's own conditions (exactly one bit per network, network title states the alarm text).
    fix: Either restructure to satisfy C-501 as written, or - if this packed/summarized form is intentionally fine - propose it as a documented exception in docs/06-lad-conventions.md rather than leaving the rule and the practice disagreeing.

FILE: ir/reference/PerimeterSafetyAlarms.ir
  BLOCK: PerimeterSafetyAlarms

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, clean
  C-301: checked, 2 finding(s)
  C-501: checked, 1 finding(s)
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Warn] C-003 (block level)
    Block name 'PerimeterSafetyAlarms' does not start with the required 'FC_' prefix for a FC.
    fix: Rename to 'FC_PerimeterSafetyAlarms' (coordinate with any callers/references first).
  [Error] C-301 network 1
    Network 1 writes 8 slice-access bit(s) (AlarmWords.AlarmWord1.%X0, AlarmWords.AlarmWord1.%X1, AlarmWords.AlarmWord1.%X2, AlarmWords.AlarmWord1.%X3, AlarmWords.AlarmWord1.%X4, AlarmWords.AlarmWord1.%X5, AlarmWords.AlarmWord1.%X6, AlarmWords.AlarmWord1.%X7) without satisfying the C-501 alarm-word exception (exactly one bit, network titled).
    fix: Split into one network per alarm bit, each titled with its own alarm text, or move this logic into a self-identified data-handling/comms block (C-105).
  [Warn] C-501 network 1
    Network 1's slice-access alarm bit(s) don't satisfy C-501's own conditions (exactly one bit per network, network title states the alarm text).
    fix: Either restructure to satisfy C-501 as written, or - if this packed/summarized form is intentionally fine - propose it as a documented exception in docs/06-lad-conventions.md rather than leaving the rule and the practice disagreeing.

FILE: ir/reference/ScaleValue.ir
  BLOCK: ScaleValue

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, clean
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Warn] C-003 (block level)
    Block name 'ScaleValue' does not start with the required 'FC_' prefix for a FC.
    fix: Rename to 'FC_ScaleValue' (coordinate with any callers/references first).

FILE: ir/reference/SignalConditioning.ir
  BLOCK: SignalConditioning

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, clean
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Warn] C-003 (block level)
    Block name 'SignalConditioning' does not start with the required 'FC_' prefix for a FC.
    fix: Rename to 'FC_SignalConditioning' (coordinate with any callers/references first).

FILE: ir/reference/ThresholdAlarms.ir
  BLOCK: ThresholdAlarms

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, clean
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Warn] C-003 (block level)
    Block name 'ThresholdAlarms' does not start with the required 'FC_' prefix for a FC.
    fix: Rename to 'FC_ThresholdAlarms' (coordinate with any callers/references first).

FILE: ir/reference/TimerSample.ir
  BLOCK: TimerSample

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, 1 finding(s)
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Warn] C-003 (block level)
    Block name 'TimerSample' does not start with the required 'FC_' prefix for a FC.
    fix: Rename to 'FC_TimerSample' (coordinate with any callers/references first).
  [Error] C-201 (block level)
    'TimerSample' has no header comment.
    fix: Add a COMMENT stating the block's purpose (and, per C-201, author/revision - not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).

FILE: ir/reference/TimingAndCalls.ir
  BLOCK: TimingAndCalls

  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-201: checked, clean
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, 2 finding(s)
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Warn] C-003 (block level)
    Block name 'TimingAndCalls' does not start with the required 'FC_' prefix for a FC.
    fix: Rename to 'FC_TimingAndCalls' (coordinate with any callers/references first).
  [Error] C-406 network 1
    Network 1 calls 'FBTimers.RunTimeTimer' as a Tonr timer - only TON is permitted.
    fix: Replace with TON plus explicit inversion/edge logic per C-406's own construction guidance (off-delay/retentive behavior built from TON, not TOF/TONR).
  [Error] C-406 network 2
    Network 2 calls 'FBTimers.HoldTimer' as a Tof timer - only TON is permitted.
    fix: Replace with TON plus explicit inversion/edge logic per C-406's own construction guidance (off-delay/retentive behavior built from TON, not TOF/TONR).

SUMMARY: 7 file(s), 16 finding(s) (7 error, 9 warn)
```

Notes on the mechanical pass (my words, the verbatim output above stands as what the tool said):
- C-102/C-401/C-404 report `checked, vacuous` on every network-bearing file — reported exactly as stated: they cannot fire against current IR capability, and are **not** upgraded to "verified clean."
- **Converter bug/gap report (not a re-derivation of tool output):** C-003's instance-DB sub-clause (`iDB_<FBName>_<Instance>`, doc 06 C-003) appears unenforced — `iDB_PusherControl` and `iDB_ShredderSequencer` carry no `_<Instance>` segment (contrast `iDB_MotorFwdRevSystem_Shredder`, which does) yet both report `C-003: checked, clean`. Because the tool reports these files `checked`, I do not file an AI finding on that rule for those files; I report the gap for the converter backlog and the naming observation rides with it.

## AI findings

### FC_ControlMain (generated)

- [C-308, error, bucket B] Network 5 ("PusherControl Wiring"): cyclic scan-copies from `DB_Settings` into `iDB_PusherControl`'s instance-UDT settings members — eight `MOVE`s plus one `COIL`, every scan. This is the exact shape doc 06 names as the trap: each setting exists in two homes with a copy in between, so any faceplate edit of the pusher's own UDT settings silently reverts one scan later.
  Evidence: `MOVE(EN := TRUE, IN := DB_Settings.PusherEndTravelTimeout) => iDB_PusherControl.IO.EndTravelTimeout` (line 57; likewise lines 58–64 for `ParkedTimeout`, `EndTravelHoldTime`, `PumpRunOnTime`, `JogWarningTime`, `PressureTripConfirmTime`, `PressureClearResumeDelay`, `PressureTripCountThreshold`) and `COIL iDB_PusherControl.IO.Fitted := DB_Settings.PusherFitted` (line 46).
  Rule text basis: C-308 — "the same applies to settings members inside an instance UDT … logic never writes them, **and orchestrating FCs never scan-copy values into them**: a cyclic `MOVE` from `DB_Settings` over a faceplate-written UDT member silently reverts every HMI edit one scan later (the test-project001 `FC_ControlMain` trap — the setting *exists twice* with a copy in between, the worst of both homes)."
  Suggested fix: delete the nine copies; make the instance UDT the single home (C-307), with commissioning defaults as iDB start values (C-309), and remove the duplicated `DB_Settings.Pusher*/Pressure*` members. (Stage-gates records this rework as a queued S6 request — context, not a resolution.)
- [C-103, warn, bucket B] Network 3, `IO.FaultFB` cross-block writer conflict: this FC plain-COILs `iDB_MotorFwdRevSystem_Shredder.IO.FaultFB` from the field buffer every scan while the FB `RCOIL`s the same bit on `FaultReset` (FB network 11) — a reset coil with no paired set coil anywhere, its target rewritten each scan by a different block.
  Evidence: `COIL iDB_MotorFwdRevSystem_Shredder.IO.FaultFB := DB_Input.Motor_Fault` (FC_ControlMain line 34) vs `RCOIL IO.FaultFB := IO.FaultReset` (FB_MotorFwdRevSystem line 184); grep confirms no `SCOIL` targets `FaultFB` anywhere.
  Rule text basis: C-103 — "Set/Reset pairs in the same block, ideally adjacent networks."
  Suggested fix: pick one mechanism — if `FaultFB` is a live field mirror, drop the RCOIL's reliance (it is overridden next scan); if it is an event latch, set it edge-wise instead of level-copying.
- [C-201, error, bucket A — content half] Network 7 title "Output Mapping" misdescribes the network: it hands equipment outputs into the `DB_Output` buffer; "output mapping" is this project's reserved C-109/C-110 term for the buffer→physical Map FC (`FC_Outputs`, "Output Map"), so the title collides with the term of art. (Header-comment *presence* is the tool's finding above; author/revision are not capturable in this IR export — stated, not assumed either way.)
  Evidence: `NETWORK 7 "Output Mapping"` heading a network of `COIL DB_Output.* := iDB_*.IO.*` rungs.
  Rule text basis: C-201 (title exists but must describe the network) with C-109/C-110's vocabulary ("input mapping is the first call in OB1; output mapping is the last" — the Map FCs).
  Suggested fix: retitle, e.g. "Equipment Outputs To Buffer".
- [candidate rule gap, bucket B] Two orchestrator writes permanently force interface *state* bits that the callee also writes or manages: `COIL iDB_MotorFwdRevSystem_Shredder.IO.RecentStart := AlwaysTrue` (line 30) fights the FB's own self-clearing management (`COIL IO.RecentStart := IO.RecentStart AND NOT IO.RunFwd AND NOT IO.RunRev AND NOT IO.FaultActive`, FB line 142, whose comment states the HMI is the intended setter). No C-0xx–C-5xx rule names a one-writer discipline for non-settings state bits (C-308 is settings-only; C-402 is edge bits only) — this offends the Data section's stated one-writer principle ("One-writer makes the whole class impossible and is statically checkable"). Functional consequence filed under Tier-1 below.
  Suggested fix: candidate rule — "an interface state bit has exactly one writing block"; here, stop forcing `RecentStart` and wire the intended HMI/orchestrator event instead.

### FB_ShredderSequencer (generated)

- [C-122, error, bucket A] Network 2 ("HMI Time Conversions"): every step timer's `PT` chain **starts at `DB_Settings`**, not at a settings member of this block's own interface UDT — `UDT_ShredderSequencerIO` contains no settings members at all. These are one sequencing instance's own step timings, which the rule (as reworded 2026-07-16) assigns to the owning block's UDT; nothing here is plant-wide with no single owning block.
  Evidence: `MUL(EN := TRUE, IN1 := DB_Settings.PreStartSounderTime, IN2 := 1000.0) => Time` … (lines 111–120, ten settings) → `CONVERT … => <Name>MS` → e.g. `TON(PreStartTimer, IN := IO.Step = 10, PT := PreStartSounderTimeMS)`; also `DB_Settings.ReversalCountThreshold` read directly in network 5 (line 143).
  Rule text basis: C-122 — "`PT` comes from a settings member of the owning block's own interface UDT (C-307's per-instance scope — the block's faceplate tunes its own sequence); `DB_Settings` holds a stage timing only when it is genuinely plant-wide with no single owning block."
  Suggested fix: move the sequencer's timing/threshold settings into `UDT_ShredderSequencerIO` (start values as commissioning defaults) and read `IO.*` in network 2.
- [C-122, error, bucket A] Step 40 (WaitForwardRun, network 10) has no max-dwell timer of its own: its only non-stop exits are `IO.ShredderRunFwdFB` (success) and `IO.MotorFaultActive` (the motor FB's cross-block fault, whose fail-to-run time setting is currently unconfigured — see Tier-1 #6). If forward feedback never comes and the motor FB does not fault, the sequence dwells in step 40 indefinitely with no fault of this block's own.
  Evidence: network 10 contains only three `MOVE` transitions, no `TON` (lines 166–169), against every other timed step owning a dedicated timer.
  Rule text basis: C-122 — "A step's own maximum dwell gets a dedicated timer … `Q` always drives a fault (C-123), never a silent 'carry on.'"
  Suggested fix: add a `WaitForwardRunTimer` (Static TON, `IN := IO.Step = 40`, PT from an interface-UDT setting) driving a latched fault plus an abort transition to 0.
- [C-202, warn, bucket C — judgment] Two network comments point at member comments that do not exist in this export: network 4's "see ReversalCount's own comment for why this is a re-arming window" and network 12's "see OvercurrentTripped's own comment for the AlwaysTrue placeholder gap". Grep over the corpus finds zero member-level COMMENT tokens on any DB/TYPE/Static member — both pointers dangle, so the *why* they delegate is unreadable in the review medium.
  Evidence: `COMMENT "…see ReversalCount's own comment…"` (line 136); `COMMENT "…see OvercurrentTripped's own comment for the AlwaysTrue placeholder gap…"` (line 180); no `COMMENT` on any member line in `FB_ShredderSequencer.ir`/`iDB_ShredderSequencer.ir`.
  Rule text basis: C-202 — "Comments say *why*, not what (the rungs already say what)" — a why-comment that defers to absent documentation doesn't say why.
  Suggested fix: inline the two rationales into the network comments (and treat member-comment persistence as the separate toolchain question it is).
- Sequencing cluster status (declared, per skill): C-118 clean (`Step : Int` is `UDT_ShredderSequencerIO.Step`, caller-visible — TYPE file line 15; not a bare Static, not a `DB_Controls`/`DB_Settings` member). C-119 clean on stop/fault paths (every step 10–60 has a `StopCmd → 0` and fault exits to 0; idle is 0) — the *restart* leg depends on the missing C-124 machinery (project-scope finding, Cross-block tables). C-120 clean (steps 0–60 in tens; legend in header). C-121 clean: all 18 `=> IO.Step` writes are `MOVE`s whose `EN` contains `IO.Step = <from> AND <condition>` (grep-enumerated, lines 146–193); multi-exit steps are mutually exclusive **by construction** (`StopCmd` / `NOT StopCmd AND fault` / `NOT StopCmd AND NOT fault AND <progress>` — complementary guards visible in the expressions; no exclusivity judgment needed). C-123 clean in-block (faults latched, cleared only by named `FaultReset`, explicit abort transitions; no hold bit exists — stop aborts instead, which is a design choice not a breach). C-125 clean (legend present; `DischargeConveyorTimeoutFault`/`ShredderBlockedFault` live in the interface UDT). C-113 stated-paradigm presence: satisfied — header says "through a stepped cycle" with a step legend (paradigm *choice* reasoning: Judgment section).

### FB_PusherControl (generated)

- [C-122, error, bucket B] The step timers' `PT` chains ultimately **start at `DB_Settings`**: in-block they trace to `IO.*` settings (compliant on single-file view), but whole-project view shows those UDT members are scan-copied shadows of `DB_Settings.Pusher*/Pressure*` (FC_ControlMain network 5 — the C-308 finding above). Same root cause, second rule: the pusher's own step timings are HMI-homed in `DB_Settings`, not in the faceplate-owned UDT.
  Evidence: `TON(EndTravelTimer, IN := IO.Step = 10, PT := EndTravelTimeoutMS)` (line 140) ← `MUL(EN := TRUE, IN1 := IO.EndTravelTimeout, …)` (line 107) ← `MOVE(EN := TRUE, IN := DB_Settings.PusherEndTravelTimeout) => iDB_PusherControl.IO.EndTravelTimeout` (FC_ControlMain line 57).
  Rule text basis: C-122 (as quoted above) — the skill's own instruction: "follow the data chain through MS shadows; a PT whose chain starts at `DB_Settings` fails unless that timing is genuinely plant-wide."
  Suggested fix: same as the FC_ControlMain C-308 fix — one home (the UDT), no copies; this finding closes automatically with that one.
- [C-123, error, bucket C — judgment] Network 9: `ParkedTimeoutFault` is latched and `FaultReset`-cleared but drives **no recovery/abort transition** — step 30 simply keeps retracting. The comment argues this deliberately ("if genuinely stuck retracting there's no safer step to force … alarm-only by design, awaiting a human"), and it is not a *silent* freeze (the fault alarms via `DB_Alarms.ShredderAlarm0.%X6`). The rule's letter and this design disagree — flagged for owner ruling, not adjudicated. judgment
  Evidence: `COIL IO.ParkedTimeoutFault := ParkedTimer.Q OR IO.ParkedTimeoutFault AND NOT IO.FaultReset` (line 154) with only `MOVE(EN := IO.Step = 30 AND IO.HomeLimit, IN := 0) => IO.Step` (line 155) as the step's exit.
  Rule text basis: C-123 — "A fault is latched, cleared only by a named `FaultReset`, handled by an explicit transition to a defined recovery/abort step — never a silent freeze."
  Suggested fix (if the owner rules with the letter): treat persisting timeout as an abort-to-0-with-fault (blocking restart until reset), or document this case as a rule exception.
- Sequencing cluster status (declared): C-118 clean (`UDT_PusherIO.Step`, TYPE file line 4). C-119 clean on stop/fault paths — including the good not-at-home-at-idle → Retract recovery (network 6, documented); restart leg depends on missing C-124 machinery (project-scope). C-120 clean (0/10/20/30 + legend). C-121 clean: all 8 `=> IO.Step` writes are compliant `MOVE`s (grep-enumerated, lines 135–155); step 10's four exits and step 0's two exits are mutually exclusive by construction (`Mode = 0`/`<> 0`, `Q`/`NOT Q`, `Blocked`/`NOT Blocked`, `HomeLimit`/`NOT HomeLimit`). Step 20's `HoldTimer` and the absence of a separate step-20 watchdog is acceptable within the rule's purpose — the step's exit *is* a timer, so it cannot stall (noted, not flagged). C-123's hold/fault split is exemplary: `PressureHold` (self-clearing hold, never writes `Step`, gates `ExtendDemand` only) vs `Blocked` (latched fault from `PressureTripCount >= IO.PressureTripCountThreshold`) — they compose exactly as the rule describes. C-125 clean. C-107/C-402 clean (`PressureConfirmEdge`/`PressureConfirmEdgeMem` — dedicated, one writer each). C-113 stated-paradigm presence: satisfied (header cites C-118–C-125, step legend, mode legend).

### FB_MotorFwdRevSystem (imported-real — all items are documented context and calibration, never fix demands)

- [C-402, error, bucket A — context] `HandPosEdge` has **two** writers serving two different signals: line 147 computes InHand's rising edge, line 149 computes InHand's *falling* edge into the same bit (the second, later coil wins each scan). `HandNegEdge` is declared (Static line 105) and never written or read — the falling-edge write looks intended for it. Cross-reference risk to generated code: none (grep — no block outside this FB reads `HandPosEdge`).
  Evidence: `COIL HandPosEdge := IO.InHand AND NOT RisingEdgeFlags[3]` (147) then `COIL HandPosEdge := (IO.InHand OR NegitiveSignalEdge[2]) AND NOT IO.InHand` (149); network 6 consumes `… AND NOT HandPosEdge AND NOT HandPosEdge` (153 — the same term twice).
  Rule text basis: C-402 — "Edge memory bits are dedicated, never reused."
- [C-103, warn, bucket A — context] Reset-only S/R bits: `IO.HandIntervention` (RCOIL line 136) and `IO.HandStartSignal` (RCOILs lines 137, 143) have no set side in the PLC — the network comments document the HMI as setter ("Must Have Set Bit (#IO.HandIntervention) On Hand Control Buttons Press/Release On HMI"), a real HMI-wiring contract the motor-dol pattern also records. In test-project001 no HMI exists and `InHand` is wired constant-false, so these paths are dormant. `IO.FaultFB`'s reset-only status is the FC_ControlMain finding above. In-block pairs are clean and adjacent: `Pasue` S(116,119)/R(122,124), `StartTimer` S(117,120)/R(125), `CycleDelay` S(123)/R(126,127), `IO.StopMotor` S(169)/R(172).
  Rule text basis: C-103 — "Set/Reset pairs in the same block, ideally adjacent networks."
- [C-403, error, bucket B — context feeding a project-scope finding] Complete S/R-written-bit inventory for this block: `Pasue`, `StartTimer` (TEMP), `CycleDelay`, `IO.HandIntervention`, `IO.HandStartSignal`, `IO.StopMotor`, `IO.FaultFB`. None appears in a dedicated startup-reset block because **no such block exists in the project** — that absence is filed against the generated integration (Cross-block tables), not against this block; its own S/R usage stays context. Also RETAIN context: the whole `IO` struct and `HrTotaliserTimer` are RETAIN (iDB lines 8, 86).
  Rule text basis: C-403 — "Every bit written by any S/R mechanism (except documented settings/parameters) must also appear in the dedicated startup-reset block … executed once at PLC startup."
- [C-107, info, bucket A — context] Edge arrays hold the one-writer discipline: `RisingEdgeFlags[0..3]` and `NegitiveSignalEdge[0],[2]` each have exactly one writer (grep: lines 118, 148, 170, 192, 121, 150), statically indexed. `NegitiveSignalEdge[1]` is a dead element (never written or read).
- Calibration context on the tool's findings: the motor-dol pattern (`patterns/motor-dol/pattern.md` §Behaviour 12 and 14) documents this FB family's TONR hours-totaliser and 3-bit packed alarm word as **known deviations — "not the template to copy"** — so the tool's C-406/C-301/C-501 findings on networks 13/15 land on already-recorded pattern-vs-rule tensions (Judgment section), not new discoveries. The misspellings `Pasue`/`NegitiveSignalEdge` are typos in an imported-real block — recorded as context, **not** C-006 findings (calibration honored).

### FC_Inputs (generated)

- No AI findings beyond the tool's C-201 header finding. Pattern conformance verified against `patterns/input-mapping` and `patterns/db-inputs`: every point carries the full IO-test-override rail (`AlwaysTrue AND NOT DB_Input.Test[0] AND <DI> OR AlwaysTrue AND DB_Input.Test[n]`), test indices 1–22 are unique (no collisions — the pattern's documented real-data bug avoided), DI4's NC polarity is absorbed at the map with a comment stating exactly that (the pattern's negated variant; C-304-aligned), spares map to `SpareDI[1..7]`. The rail idiom and `Test[n]` arrays are pattern-sanctioned — not flagged (calibration).

### FC_Outputs (generated)

- No AI findings beyond the tool's C-201 header finding. Symmetric pattern-conformant rail on all 18 physical outputs; spares read `SpareDQ[1..7]` (never written — de-energized by construction). Note: `Test[0]` is IO-test/commissioning override, **not** C-111 simulation (the input-mapping pattern's own recorded correction) — simulation absence is the project-scope cluster finding, not a defect of this block's rungs.

### Main / OB1 (generated)

- No AI findings beyond the tool's C-201 header finding. C-109/C-110 verified clean — see Cross-block tables.

### FC_AlarmsMain (generated)

- [C-502, warn, bucket B] `FC_AlarmsMain` contains the nine alarm rungs directly instead of calling one monitoring FC per category, and `FC_GeneralAlarms`/`FC_EStopAlarms` do not exist anywhere in the export (Glob/grep over `ir/test-project001/`). Per the skill this is flagged as an **owner scale-down question** for a small scratch project, not adjudicated here — noting also that this IO set contains no E-Stop input for an `FC_EStopAlarms` to monitor.
  Evidence: networks 1–9 are all `COIL DB_Alarms.ShredderAlarm0.%Xn := <fault>`; no `CALL` statements.
  Rule text basis: C-502 — "`FC_AlarmsMain` … calls one monitoring FC per monitored function/category. Categories vary per project, but `FC_GeneralAlarms` (catch-all) and `FC_EStopAlarms` always exist."
  Suggested fix (if the owner holds the letter): move rungs into `FC_GeneralAlarms` (or per-equipment FCs) called from here; otherwise record the scale-down.
- C-501's own conditions verified clean beyond the tool: exactly one alarm bit per network, each network titled with the alarm text (tool: `C-301: checked, clean; C-501: checked, clean`). C-505 title-proxy: all nine titles follow `<Equipment> - <fault> - <action hint>` (e.g. `"Pusher - Blocked, Too Many High Pressure Trips - Reset Required"`) — structure compliant; they use plain hyphens rather than the rule's em-dashes (cosmetic, noted only); actual HMI alarm texts are not in this medium (Not checkable). C-504/C-503 items: Judgment section.

### DB_Input / DB_Output (generated)

- [C-001, error, bucket A] Buffer members are `Snake_Case` with underscores — C-001's 2026-07-16 revision makes members short PascalCase with **no underscores** (only the physical-IO *tag* format keeps underscores, and these are DB members, not IO tags). The db-inputs pattern's own examples use PascalCase (`OverbandMagIsoFB`).
  Evidence: `DB_Input`: `Control_Healthy`, `Cycle_Start`, `Cycle_Stop`, `Shredder_Run_Fwd_FB`, `Shredder_Run_Rev_FB`, `Motor_Fault`, `Hopper_Level_High`, `Infeed_Conv_Running`, `Discharge_Conv_Running`, `Pusher_PowerPack_Running`, `Pusher_Home_Limit`, `Pusher_Full_Travel_Limit`, `Pusher_High_Pressure`, `Downstream_Running`, `Pusher_Local_Remote` (15); `DB_Output`: `Run_Shredder_Fwd`, `Run_Shredder_Rev`, `Run_PowerPack`, `Run_Infeed_Conv`, `Run_Discharge_Conv`, `Pusher_Extend`, `Pusher_Retract`, `In_Cycle`, `Enable_Upstream`, `PreStart_Sounder`, `Motor_Fault_Reset` (11).
  Rule text basis: C-001 — "Variables/UDT members: short PascalCase … Underscore-free member names; the physical-IO tag format below keeps its underscores by design."
  Suggested fix: rename to PascalCase at next touch (stage-gates records the owner's rename-at-next-touch disposition — context, declared source; the finding stands as a fact of the current corpus).
- Structure otherwise pattern-conformant (canonical member order `Test` → `SpareDI`/`SpareDQ` → named points; 1:1 `Test` sizing `[0..22]`/`[0..18]`) — the flat per-point members are the documented buffer-DB mapping shape, **not** a C-302 family (calibration honored).

### DB_Settings (generated)

- C-307 home findings are tabled below (every member is instance-owned — the table is the finding). Header comment is substantive and cites its own rules; all members RETAIN (C-307's default ✓). C-309 anti-finding honored: no clamp/range-validation findings raised anywhere in this report; the unset start values are *documented* in the header ("genuinely unconfigured pending real numbers … not a guess") and appear only as a Tier-1 commissioning note, not a settings-validation complaint. `OvercurrentSetpointMedium/High` are consumed by nothing (grep: declared only) — dead members tied to the overcurrent placeholder (Tier-1 #1).

### DB_Controls (generated)

- Clean. Members (`PusherMode`, `PusherManualCycleCmd`, `PusherJogExtendCmd`, `PusherJogRetractCmd`, `FaultReset`) are all operator commands/mode selections; grep confirms no logic writes any `DB_Controls` member (C-306, C-308(a) both hold).

### DB_Alarms / DB_AnalogInput (generated)

- `DB_Alarms` clean under the C-501 DB-side residual — see Cross-block tables. `DB_AnalogInput` clean; its header documents the deliberately-unmapped `ShredderMotorCurrent` (no AI hardware) — the buffer is never written by any Map FC, so the value handed to the sequencer is constantly 0.0 (Tier-1 #9, documented).

### UDT_PusherIO / UDT_ShredderSequencerIO / MotorFwdRevIOSet (TYPE files)

- [C-201, error→owner question, bucket A] **hand-checked — tool reports NotApplicable for this content kind.** None of the three TYPE files carries a `COMMENT` line. C-201's text says "every **block** has a header comment"; whether a PLC data type is a "block" for this rule is an interpretation question — filed as a **candidate rule gap** (should C-201 or a new rule require a UDT header comment?) rather than asserted as a violation. Separately noted: no member on any TYPE/DB file in the corpus carries a member comment (interface-member commenting is C-605, review-simplicity's rule — routed there).
  Evidence: `TYPE UDT_PusherIO` / `TYPE UDT_ShredderSequencerIO` / `TYPE MotorFwdRevIOSet` — no `COMMENT` token in any.
- Member naming (C-001, hand-checked, same NotApplicable label): all three UDTs' members are underscore-free PascalCase — clean. (`MotorFwdRevIOSet` itself lacks the `UDT_` prefix — a C-003 matter on a TYPE file, which the tool cannot check in Phase 1: hand-checked, imported-real → context, not a demand; the generated UDTs carry the prefix correctly.)

### iDB_PusherControl / iDB_ShredderSequencer / iDB_MotorFwdRevSystem_Shredder (generated)

- No AI findings beyond the tool's (C-201 headers; TONR member in the motor iDB). Naming: the missing `_<Instance>` suffix on the two generated iDBs is covered by the converter bug report in the mechanical-pass notes (tool reports C-003 `checked, clean`, so the tool owns the rule; the observation rides with the bug report). RETAIN facts feeding the startup cluster: `IO` structs RETAIN in all three (so `Step`, run commands, holds and latches persist a power cycle); `iDB_MotorFwdRevSystem_Shredder.IO.ReverseDelay = 8.0` is the only timing start value — `FTTime`/`EnableUPSTime`/`ShutdownTime`/`ReverseIgnoreFT` have none (Tier-1 #6).

### DefaultTagTable (generated project surface; legacy leftovers)

All items **hand-checked — tool reports NotApplicable for this content kind.**

- [C-001, error, bucket A, hand-checked] 52 legacy `Tag_1`…`Tag_54` entries plus `AirStarWord0IN/2IN/0OUT/2OUT` follow no naming layer of C-001 (neither the physical-IO format nor meaningful PascalCase names), sit at addresses outside this project's IO (`%I50x`, `%IW6x`, `%Q50x`), and are referenced by **no block** (grep over all test-project001 bodies: zero hits). Housekeeping-grade: dead sandbox inventory in the committed corpus.
  Rule text basis: C-001 — "Tag naming is layered: … Physical IO tags: `<DI/DO/AI/AO><n>_<Equipment>_<Signal>`."
  Suggested fix: delete the unreferenced legacy tags (engineer action in TIA).
- [C-005 tension, owner ruling, hand-checked] `Clock_0.5Hz` contains a literal dot — a C-005 breach on its face, but it is Siemens' own clock-memory default name: recorded per the skill's calibration as a **tension for the owner** (rename vs tolerate the vendor default), never a fix demand. The table's own name "Default tag table" (spaces) is the same vendor-default class.
- Clean side: the 40 `DIn_*/DQn_*` tags follow C-001's physical-IO format exactly, all carry English comments (C-006 ✓), equipment codes (`SYS/SHR/PSH/IFC/DIS/HPR/SPR`) are used consistently across DI and DQ (C-004 consistency half ✓). `FirstScan` and `AlwaysTrue` are well-named utility bits; `FirstScan` is referenced by nothing (relevant to the startup cluster below); `AlwaysTrue` is read-only everywhere (grep: never a write target) — its truth depends on the CPU system-memory-byte hardware setting, which this medium cannot verify (Not checkable).

### Reference corpus (all imported-real — context/calibration entries; tool findings above stand)

- `AlarmWords`: [C-501-residual, warn, bucket B — context] words are named `AlarmWord0..3`, not `<Category>Alarm0` — the generic naming predates the rule; calibration context. DB-side packing (Words, extend-by-word) otherwise matches the rule's shape.
- `NodeStatusAlarms` / `PerimeterSafetyAlarms`: the packed-word shape (15 and 8 bits in one network) is the standing rule-vs-practice open question stage-gates carries — owner-ruling item, not re-adjudicated. `PerimeterSafetyAlarms`' comment documents polarity and the `%X0` summary bit well (title/comment added by S3 AI work).
- `EquipmentStatus`: [C-302, warn, bucket B — context] textbook flat per-equipment tag family (`Equipment01Fault` … `Equipment21CommsError`, plus `Ready`/`Running` variants) that the rule says should be one UDT × N instances — this is precisely the corpus shape the rule exists to prevent; kept as calibration contrast, no demand.
- `FBTimers` + `DB_Timers`: [C-407, warn, bucket B — context] standalone timer instances live in **two** homes — `DB_Timers` (`SampleTimer0..2`, individually named, the rule's shape) and `FBTimers` (`RunTimeTimer` TONR, `HoldTimer` TOF) — a scattered second timer DB, against "All standalone timers outside equipment FBs live in the single shared `DB_Timers`."
- `CommsProcessData`: comms DB (`SequenceStep : USInt` is declared but no block in the corpus writes any Step member — sequencing cluster n/a at this scope, stated explicitly). Comms/data-handling naming and packing are the C-105/C-301 fenced territory by design.
- `DataHandling`: properly fenced per C-105's two conditions (name + header comment "Data-handling reference examples…") — the tool's C-301 exemption confirmed appropriate on content.
- `BooleanExtras`: `Motor1Latched` S/R pair set/reset in adjacent rungs of one network (C-103 ✓). Its S/R bit is a TEMP in a demo FC; the reference corpus is not a runnable project, so the C-403 startup-reset cross-check is n/a here (stated, not silently passed).
- `TimerSample`: reads `TempControlBools`/`TempControlDInt`, DBs **not present** in `ir/reference/` — cross-reference gap; noted under scope limits. Timer chaining (timer 2 gated by timers 0 and 1's `Q`) is C-408/C-409-conformant.
- `ScaleValue`, `SignalConditioning`, `ThresholdAlarms`, `TimingAndCalls`: no AI findings beyond the tool's. `ThresholdAlarms` hardcodes its thresholds (800/200/650/50) as literals rather than settings — acceptable in a TEMP-only demo FC; in a real block C-307 would ask where those live (context line only).

## Cross-block tables

**C-308 — the one-writer table (three sweeps, test-project001 scope):**

| Sweep | Result |
|---|---|
| (a) Logic writes targeting `DB_Settings.*` | **None** (grep `(COIL\|SCOIL\|RCOIL\|=>) DB_Settings.` — zero hits). Clean. |
| (b) Logic writes targeting instance-UDT settings members | **FC_ControlMain network 5**: `=> iDB_PusherControl.IO.{EndTravelTimeout, ParkedTimeout, EndTravelHoldTime, PumpRunOnTime, JogWarningTime, PressureTripConfirmTime, PressureClearResumeDelay, PressureTripCountThreshold}` + `COIL iDB_PusherControl.IO.Fitted`. No FB writes its own `IO.*` settings. |
| (c) Cyclic scan-copies `DB_Settings` → instance-UDT settings | **The same nine writes — the doc-06-named trap, present.** Error finding (filed under FC_ControlMain). |

**C-307 — settings home table (owner × home):**

| Setting(s) | Lives in | Owned by | C-307 verdict |
|---|---|---|---|
| `PusherEndTravelTimeout`, `PusherParkedTimeout`, `PusherEndTravelHoldTime`, `PusherPumpRunOnTime`, `PusherJogWarningTime`, `PressureTripConfirmTime`, `PressureClearResumeDelay`, `PressureTripCountThreshold`, `PusherFitted` | **Both** `DB_Settings` and `UDT_PusherIO` (scan-copy between) | Single pusher instance | Two homes — should be UDT-only (warn; error side is C-308 above) |
| `PreStartSounderTime`, `ShredderReverseRunTime`, `DischargeConveyorTimeout`, `ReversalWindowTime`, `ReversalCountThreshold`, `ReversalRetryPauseTime`, `InfeedRestartDelay`, `InfeedToUpstreamEnableDelay`, `OvercurrentSpinUpAllowance`, `OvercurrentMediumDelay`, `OvercurrentHighDelay` | `DB_Settings` only | Single sequencer instance (its own step/stage timings) | Wrong home — belongs in `UDT_ShredderSequencerIO` (warn; PT-sourcing side is C-122 above) |
| `OvercurrentSetpointMedium`, `OvercurrentSetpointHigh` | `DB_Settings` only | (would be sequencer) | Wrong home **and** dead — consumed by nothing (Tier-1 #1) |
| `FTTime`, `EnableUPSTime`, `ShutdownTime`, `ReverseDelay`, `ReverseIgnoreFT` | `MotorFwdRevIOSet` (instance UDT) | Motor instance | **Compliant** — the imported-real block is the C-307-conformant one (calibration contrast) |

Consequence worth stating: after the queued rework every current `DB_Settings` member moves out — `DB_Settings` would be empty. Owner should confirm that end-state (or name what is genuinely plant-wide).

**C-115 — handshake vocabulary table** (site canonical taken from the motor-dol pattern + the imported FB: `AutoStartSignal`/`HandStartSignal`/`RunningFB*`/`SystemHealthy`/`InhibitMotor`/`FaultReset` in; `Run*`/`UPSEnable`/`ShutdownComplete`/`FaultActive`/`FTR`/`FTS` out — doc 06's "enable in; ready, running out" is its "e.g." form):

| FB | Enable-in | Running-feedback in | Run/Running out | Enable-out | Verdict |
|---|---|---|---|---|---|
| FB_MotorFwdRevSystem (imported-real) | `AutoStartSignal` | `RunningFBFwd/Rev` | `RunFwd/RunRev` | `UPSEnable` | Site vocabulary — consistent (context baseline) |
| FB_PusherControl (generated) | — (`Enable` exists but is a **self-written derived bit**: `COIL IO.Enable := IO.Fitted AND IO.Mode <> 0`, network 1 — a chain-vocabulary word repurposed) | `PowerPackRunningFB` (partial) | `RunPowerPack`/`Extend`/`Retract`; `Cycling` as status | — | **Deviation** (warn, bucket B): vocabulary not shared; `Enable`'s role actively collides with the chain meaning |
| FB_ShredderSequencer (generated) | `CycleStart`/`CycleStop` | `DischargeConvRunning`, `ShredderRunFwdFB` | `RunDischargeConv`/`RunInfeedConv` | `EnableUpstream` | **Deviation** (warn, bucket B): plant-level block with its own vocabulary; `EnableUpstream` is the one canonical-shaped name |

Rule text basis: C-115 — "Every equipment FB exposes the same handshake vocabulary through its UDT … so the chain wires identically everywhere." Judgment nuance: the pusher is a genuinely different equipment class (hydraulic ram) with no site pattern yet — whether the motor family's vocabulary should be forced onto it is part of the owner-ruling item below.

**C-114 — enable graph:** This project is stepped-sequence, not chained-permissive — there is no enable chain in the C-114 architecture sense (stated explicitly, not silently passed). Enable-like edges, classified:

| Edge | Kind | Direction note |
|---|---|---|
| `DB_Input.Downstream_Running` → sequencer `StopCmd`/start permissive | External enable-in (field) | Downstream-proves-first — consistent |
| Sequencer `MotorAutoStartCmd` → motor `AutoStartSignal` (via FC_ControlMain) | Enable (step-derived) | Forward |
| Sequencer `EnableUpstream` → `DB_Output.Enable_Upstream` → field | Enable-out to upstream feeder | Consistent with downstream-first startup |
| Motor `FaultActive` → sequencer `MotorFaultActive` | Status/interlock feedback — **classified interlock, exempt** | Backward, allowed by the rule's own scope |
| Pusher `JogPreStartSounder` → sequencer `PreStartSounder` OR-term | Status aggregation, not enable | — |

No cycles among enable edges (each is one-directional; the backward edges are interlock/status). The material-flow-direction half needs a process-topology artifact — **not checkable** (declared below). Motor `UPSEnable` is consumed by no other block (grep) — the canonical chain link is unused in this integration (context). **C-116/C-117: n/a** — one line: the shredder's reverse running is a sequencer *phase* (step 30) with mode exclusivity enforced inside the motor FB (`… AND NOT IO.Reverse` / `… AND IO.Reverse`), not a bidirectional material-routing section with per-direction enable chains.

**Startup/simulation cluster — C-305 (warn), C-111 (error), C-124 (error), C-403 (error) — project-scope findings against the generated integration:**

Checked as one unit, whole-project grep (`Simulation|DB_PLC|OB100` → zero hits in ir/):
- No `DB_PLC` exists, hence no `Simulation : Bool` start-value-FALSE member → **C-305 (warn) finding**.
- No simulation implementation at the mapping layer: input-map calls are not gated by `-|/|-` on `DB_PLC.Simulation`, no `FC_Simulation`, and physical-output writes have no per-point simulation de-energize term (`Test[0]` is IO-test, a different mechanism per the input-mapping pattern's recorded correction) → **C-111 (error) finding**.
- No dedicated startup-reset block (no OB100-class block anywhere; `FirstScan` tag exists, referenced by nothing): the project contains S/R-written bits (the FB_MotorFwdRevSystem inventory above) and RETAIN run-state (`IO` structs RETAIN in all three iDBs — `Step`, `RunFwd/RunRev`, holds, latched faults, edge memory persist a power cycle) → **C-403 (error) finding**; `Step`, holds, edge memory and in-progress counters (`PressureTripCount`, `ReversalCount`, `HrsRun` context) are not force-reset at restart → **C-124 (error) finding**. C-124's carve-out honored: the genuine fault latches needing human acknowledgement (`FaultActive`/`FTR`/`FTS`, `Blocked`, `ShredderBlockedFault`, timeout faults) are *deliberately not* flagged for exclusion from a future reset block.
- All four filed against the generated integration; the imported block's own S/R usage stays context. Stage-gates records the owner's prior acceptance of this omission for the demo panel ("should ideally have, do not need to fix right now") — carried as context in the Judgment section; the facts stand.

**C-407 — timer homes:** All timers inside the three equipment/sequencing FBs are multi-instance Statics in their own iDBs (Pusher 8, Sequencer 10, Motor 10 — verified in the iDB files) ✓. test-project001 has no standalone timers, so no `DB_Timers` is needed ✓ clean. Reference corpus: `DB_Timers` ✓ vs `FBTimers` scattered second home — context row (see reference entries).

**OB1 shape — C-109/C-110 (both warn):** verified clean.

| OB1 network | Call | Check |
|---|---|---|
| 1 "Input Mapping" | `CALL FC_Inputs` | Input map **first** ✓ (C-110); direct Map-FC call ✓ — documented exception (C-109, owner 2026-07-16), not re-flagged (calibration) |
| 2 "Control" | `CALL FC_ControlMain` | Area-Main wrapper ✓; calls only its own area's blocks (3 equipment CALLs) ✓ |
| 3 "Alarms" | `CALL FC_AlarmsMain` | Area-Main ✓ (its internal shape is the C-502 item) |
| 4 "Output Mapping" | `CALL FC_Outputs` | Output map **last** ✓ (C-110); documented exception ✓ |

OB1 contains only calls ✓ — reads as a table of contents.

## Judgment items for owner ruling

- **C-113 (error severity, but genuine Bucket-C judgment — the stage-gates S4 mismatch, stated per the skill):** design-intent commentary, not pass/fail. Both generated blocks pass the memory test in my reading: the pusher's limit switches mean different actions by phase (extending vs retracting vs parked — current signals alone cannot say which), and the shredder's startup (siren → discharge → reverse-clear → wait-forward → run → abort/retry) is inherently phased. Stepped sequences look like the right paradigm for both; both headers state the paradigm (step legends; "through a stepped cycle"). For owner confirmation, not my ruling.
- **C-504 (error severity, same mismatch — candidates only):** candidate missing suppression — verify intent: cause `DB_Alarms.ShredderAlarm0.%X0` (Shredder Motor fault/overload, live field mirror) → consequence `%X2` (Failed To Stop): a protection trip that drops `RunFwd` while feedback decays slower than `FTTime` would raise an FTS echo; no `-|/|-` on `%X0` plus short TON extension exists in FC_AlarmsMain networks 1/3. Weaker second candidate: a stuck limit switch can co-raise `%X3` (Both Switches) and `%X5`/`%X6` (travel timeouts). Whether these are "known consequences" is design knowledge — owner's call.
- **Packed alarm words in imported-real content** (`NodeStatusAlarms` 15 bits, `PerimeterSafetyAlarms` 8 bits, `FB_MotorFwdRevSystem` network 15's 3 bits): the standing rule-vs-practice question stage-gates carries open — is C-501's "exactly one bit per network" the rule, or is the packed form a documented exception to write in? Not adjudicated; the tool's findings stand as the letter.
- **motor-dol pattern vs C-406/C-501:** the admitted pattern's own documentation records the TONR hours-totaliser and the packed alarm word as known deviations ("not the template to copy") — a pattern-vs-rule tension already on record; C-406's action item (site-standard retentive-timer FB) is the codified resolution path. Owner ruling on timing/priority, not mine.
- **C-123 letter vs `ParkedTimeoutFault`'s alarm-only design** (FB_PusherControl network 9, documented rationale) — see the finding; rule the letter or record the exception.
- **C-502 scale-down:** no `FC_GeneralAlarms`/`FC_EStopAlarms` in a small scratch project with no E-Stop IO — enforce the letter or record a scale-down.
- **C-507 candidates for the per-alarm exception list:** `%X1` FTR, `%X2` FTS, `%X3` BothSwitches, `%X4` Pusher Blocked, `%X5`/`%X6` travel timeouts, `%X7` Shredder Blocked, `%X8` discharge timeout all mirror faults latched until `FaultReset` — effectively acknowledge-required alarms (two titles literally say "Reset Required"), while C-507's default is self-clearing. C-123 *mandates* the latching, so this is a rules-interaction to resolve by listing these as documented exceptions (or re-homing the ack at the HMI) — candidates, not violations.
- **C-503 dual-home question:** FTR/FTS exist in both the motor's per-instance `IO.Alarm` word (faceplate surface) and the category word `ShredderAlarm0.%X1/%X2` (alarm list). This is not the literally-banned shape (category words duplicated per instance), but the same fault lives in two alarm words — confirm which one the HMI alarm list will bind, and whether the double raise is intended.
- **Vendor-default names:** `Clock_0.5Hz` (dot — C-005 letter) and "Default tag table" (spaces) — rename vs tolerate Siemens defaults (calibration-mandated tension line).
- **C-115 vocabulary scope:** should the motor family's handshake vocabulary be normative for non-motor equipment FBs (pusher) and plant sequencers, or does each equipment class get its own documented vocabulary? The table above is the evidence.
- **Startup machinery disposition:** the C-305/C-111/C-403/C-124 project-scope findings stand as facts; stage-gates records the owner's demo-panel acceptance ("should ideally have, do not need to fix right now") plus a permanent gen-architecture checklist line — reaffirm or schedule.
- **C-201 for TYPE files:** does "every block has a header comment" cover UDTs? Candidate rule gap (see UDT findings).
- **DB_Settings end-state after the C-307/C-308 rework:** every current member is instance-owned; confirm an empty (or repurposed) `DB_Settings` is the intended outcome.

## Clean declarations

- **Project-wide (test-project001 + reference):** C-408 — no `.ET` appears in any expression (grep `\.ET\b`: only TON-instance member declarations); C-105 — no variable array index anywhere (grep `\[[A-Za-z_#]`: zero hits in IR bodies; every index literal; the `Test[n]` arrays are pattern-sanctioned); no `SET_BF`/`RESET_BF` anywhere; C-006 — everything English (imported-real typos recorded as context, not findings).
- **Main (OB1):** C-109, C-110 (shape table above); C-102/C-401/C-404 per tool (vacuous — reported as such, not as verified-clean).
- **FC_ControlMain:** C-127-consistent role (an orchestrating FC referencing iDBs is the rule's *intended* home for cross-instance wiring); C-304 (buffers only); C-105; C-408.
- **FB_PusherControl:** C-118, C-119 (stop/fault legs), C-120, C-121 (all 8 writes enumerated), C-125, C-402/C-107 (dedicated edge pair, one writer each), C-403 (no S/R in-block), C-127 (no `iDB_`/`CALL` — grep), C-304, C-409 (each timer times a distinct fact; run-on built from TON+inversion per C-406, which the tool reports clean), C-113's presence sub-clause.
- **FB_ShredderSequencer:** C-118, C-119 (stop/fault legs), C-120, C-121 (all 18 writes enumerated; exclusivity by construction), C-123 (in-block), C-125, C-402/C-107 (ReversalStepEdge/Mem), C-403 (no S/R), C-127 (grep — `DB_Settings` is a global DB, not a sibling instance), C-304, C-409 (Medium/High overcurrent timers are two severities of one signal, not a duplicated chain span; UpstreamEnableTimer *chains* off InfeedRunning rather than re-timing it), C-113's presence sub-clause.
- **FB_MotorFwdRevSystem (context regime):** C-127 (grep clean), C-304 (buffer-fed via wiring, no physical IO), C-408 (self-seal on `ReversalIgnoreFTTimer.IN`/`.Q` reads bits, never compares `ET`), C-409 (FaultTripTimer/FTSTimer time distinct facts).
- **FC_Inputs / FC_Outputs:** C-304 (they are the only two blocks touching `DIn_`/`DQn_` tags — project-wide grep), C-001 physical-IO tag format on all 40 IO tags, pattern conformance (unique Test indices; override on every point; DI4 NC absorbed with comment).
- **FC_AlarmsMain:** C-301/C-501 conditions (tool + content check: one bit, titled, per network), C-505 structure (title proxy), C-304.
- **DB_Controls:** C-306, C-308(a).
- **DB_Alarms:** C-501-residual — exists, packed per category Word, named `<Category>Alarm0` (`ShredderAlarm0`), 9 bits < 16 so no extension word needed yet.
- **DB_Settings:** C-309 anti-finding honored (no clamp findings anywhere in this report); RETAIN default; header content.
- **DB_Input/DB_Output:** canonical pattern structure (order, 1:1 Test sizing).
- **UDTs:** C-001 member case (all three), C-118 placement for both Step members.
- **DefaultTagTable:** DI/DQ naming layer, comments, English.
- **Reference corpus:** `DataHandling` C-105 fencing; `BooleanExtras` C-103; `TimerSample` C-408/C-409 (chained timers); `ThresholdAlarms`/`SignalConditioning`/`ScaleValue` clean beyond tool findings; `CommsProcessData` n/a sequencing cluster (no Step writer in corpus — explicit n/a line).
- **C-002 (judgment, both directories):** every block name describes its function — clean.
- **C-405 (judgment):** generated blocks use only portable basics (TON/MOVE/MUL/ADD/CONVERT/CALL/compares) — clean; the TONR/TOF matters are already C-406 tool findings, not re-cited.
- **C-104 (info, judgment):** broadly honored in generated blocks (outputs written once, at or near the end; the deliberate C-126 function-grouping of `RunInfeedConv`/`EnableUpstream` beside their own timers in Sequencer network 11 is the documented priority of grouping over strict layout — noted, no finding).
- **C-108 (judgment):** reuse posture is right — the motor is a reused proven real FB rather than fresh invention; the mapping layer instantiates the four admitted/proposed patterns; the two new FBs solve genuinely unsolved problems (no stepped-sequence pattern exists yet) with the request as their stated reason — clean.

## Belongs to review-simplicity

(One routing line each; no C-1xx/2xx-readability/6xx sweeps were performed here.)

- Ranged step-membership predicates in FB_ShredderSequencer network 14 (`IO.Step >= 30 AND IO.Step <= 50`, `IO.Step >= 20` twice) and network 15 (`IO.Step >= 20`) — C-603 territory.
- Bare vs commented `AlwaysTrue`-derived constants (FC_ControlMain network 3's four wirings; FB_ShredderSequencer network 12's commented placeholder pair) — C-604 territory (functional consequence is Tier-1 #1/#2 here).
- Absent interface-UDT member comments on all three UDTs (31 + 23 + 32 bare members) — C-605 territory; note for that reviewer: TYPE-member comments currently cannot round-trip through the converter at all, so C-605 is unsatisfiable in this medium (toolchain fact, declared).
- `Snake_Case` buffer members as cross-block idiom-consistency evidence — C-607 (the same facts are C-001 evidence here; shared facts allowed, one rule one home).
- The two "HMI Time Conversions" batch networks (Pusher network 3, Sequencer network 2) against C-126's HMI-Times exception condition — both carry the required one-pair-per-timer scheme comment (fact recorded); the exception-compliance ruling is that reviewer's.
- Near-identical cycle-launch expression appearing in both FB_PusherControl network 4 (`MOVE` EN, with `OR IO.FaultReset` tail) and network 6 (transition) — C-601 named-condition territory.
- One-reading/mega-rung concerns in FB_MotorFwdRevSystem networks 2 and 14 (imported-real) — C-101/C-602/one-reading-test territory.

## Tier-1 candidates for the functional review

(Evidence-carrying observations; no rulings. Items on the imported-real block are verify-against-TIA context, per hard rule 7's posture.)

1. **Overcurrent protection chain entirely inert (generated, documented placeholder):** `OvercurrentMediumTimer`/`OvercurrentHighTimer` have `IN := … AND NOT AlwaysTrue` (permanently false, FB_ShredderSequencer lines 182–183) → `OvercurrentTripped` never true → step 60 unreachable → `ReversalCount` (edge-counted on step-60 entries) stays 0 → `ShredderBlockedFault` (network 5) can never latch. Consequently `IO.ShredderMotorCurrent` is wired in but read nowhere (grep), and `DB_Settings.OvercurrentSetpointMedium/High` are dead. The N12 comment and DB_AnalogInput header document the gap; the full knock-on (blocked-fault protection also dead) is worth the functional register.
2. **`RecentStart` forced TRUE every scan** (FC_ControlMain line 30) against the FB's own self-clearing management (FB line 142) and its stated HMI-pulse contract (FB network 4 comment) — restart gating semantics defeated; motor restart permission is permanently armed.
3. **`IO.FaultFB` cross-block write conflict** (FC_ControlMain line 34 plain-COIL vs FB line 184 RCOIL): the reset's effect lasts at most until the next scan's copy; harmless only if the field fault relay itself clears via `DQ11_SHR_FaultReset` — verify the intended reset path.
4. **`HandReverse` dead wiring:** written `NOT AlwaysTrue` (FC_ControlMain line 37), declared in the UDT, read by nothing (grep) — the member has no effect.
5. **`DB_Input.Pusher_Local_Remote` mapped but never consumed** (FC_Inputs line 31; no reader anywhere) — the DI16 selector currently does nothing.
6. **Motor timing settings unconfigured:** `iDB_MotorFwdRevSystem_Shredder.IO.FTTime/EnableUPSTime/ShutdownTime/ReverseIgnoreFT` carry no start values (only `ReverseDelay = 8.0`) → `FTTimeMS` = 0 → `FaultTripTimer` PT = 0 → FTR would latch effectively immediately on any real start attempt with normal feedback delay. (`DB_Settings`' unset members are documented as pending; the iDB ones are not covered by that comment.)
7. **Imported-real internals (context; verify in TIA before acting):** (a) hours counter provably cannot increment as modeled — network 13's `COIL RisingEdgeFlags[2] := HrTotaliserTimer.Q` executes before the `MOVE`/`ADD` whose EN requires `Q AND NOT RisingEdgeFlags[2]` (COIL kind-precedes MOVE per ir/SPEC.md's statement-kind ordering, which mirrors runtime), so the EN is false whenever Q is true; (b) `HandPosEdge` double-write annihilates the rising-edge value and `HandNegEdge` is never written (likely the intended target of line 149); network 6's `NOT HandPosEdge AND NOT HandPosEdge` duplicates one term; (c) `StartTimer` is a TEMP used as an S/R latch — per-call stack storage with no initialization; (d) `NegitiveSignalEdge[1]` is a dead array element.
8. **Sequencer `IO.InCycle` is a dead member** — written nowhere; superseded by FC_ControlMain network 7's field-feedback rung per its comment (removal deferred to the queued interface rework). Documented; carried for completeness.
9. **`DB_AnalogInput.ShredderMotorCurrent` never written by any Map FC** (documented pending real AI hardware) — the sequencer receives a constant 0.0; harmless only while item 1's placeholder also stands.

## Not checkable here

- **C-112 (error):** needs the WinCC HMI artifact — structurally out of this pipeline's reach (stage-gates S4); an error-severity rule this review can never validate. Said exactly that.
- **C-303 (error):** optimized-vs-standard block access is a TIA block property the IR medium does not carry.
- **C-506 (warn):** severity-class assignment needs the project alarm list (none exists for test-project001).
- **HMI-side halves:** C-503 (faceplate binding), C-505 (actual alarm texts — titles checked as proxy only), C-125 (what the HMI displays), C-307 (settings pages) — not in this medium.
- **C-114's material-flow half:** needs a process-topology artifact; the in-PLC enable edges were tabled and are acyclic, but flow-direction consistency is asserted only as commentary.
- **C-004's compliance half:** needs the frozen equipment-identifier list — not provided; only the consistency half was checked (clean, with the "Shredder Motor" vs "Shredder" observation noted as commentary).
- **C-201's author/revision half:** not capturable in this IR extraction (block header attributes are dropped by the exporter) — stated for every header finding rather than assumed satisfied or violated.
- **`AlwaysTrue`/`FirstScan`/`Clock_0.5Hz` semantics:** depend on the CPU's system/clock memory-byte hardware configuration, which the IR medium does not carry — every mapping rung's correctness is conditional on that config being enabled.
- **Reference-corpus scope limit:** `ir/reference/` is a reference collection, not a whole project export — `TimerSample`'s `TempControlBools`/`TempControlDInt` DBs are absent, no OB1 exists, and no alarm-category architecture exists to check C-502/C-109/C-110/C-111/C-305/C-403 against; those Group 2 rules are checkable only at test-project001 scope and are declared n/a (not silently passed) for the reference corpus.
- **C-102/C-401/C-404:** the tool reports these `checked, vacuous` (cannot fire against current IR capability) — carried exactly as the tool states them, not as verified-clean.
