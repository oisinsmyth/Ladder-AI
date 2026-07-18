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

> **Forward-note (2026-07-18, F-1 fix):** one *status-line count* above shifts against the current
> tool and is expected, not drift. `FB_MotorFwdRevSystem`'s C-301 status read `checked, 2 finding(s)`
> here because the reporter counted the co-emitted C-501 finding under C-301 (the F-1 bug); the fixed
> tool reads `checked, 1 finding(s)`. The **finding list, per-rule severities/locations, and every
> SUMMARY total are unchanged** — only that one displayed count. This evidence is a verbatim record
> of the tool as it was on 2026-07-16 and is left as-is; anyone re-running `converter review` against
> the fixed tool and diffing should treat that single count line as the intended F-1 correction.

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

## 3. Blind run report (verbatim) — moved to docs/evidence/

The full verbatim blind-run report (~951 lines: the fresh-context
agent's unedited output, including the raw `converter review` mechanical dump) is
preserved at `docs/evidence/review-conventions-blind-run-2026-07-16.md`, split out 2026-07-18 (FI-19) to keep this note
focused on the §1/§2 comparison and verdict. It is kept there verbatim and unedited —
the property that makes it usable as drift-check and blindness evidence.
