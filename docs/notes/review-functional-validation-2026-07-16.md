# review-functional skill — two-phase blind validation against the test-project001 corpus (2026-07-16)

**What this is.** Build-order step 4 of `docs/15-generation-pipeline.md`: the
`.claude/skills/review-functional/SKILL.md` reviewer plus the first requirements register
(`gen/test-project001/requirements.md` — the `requirements.md` format's defining instance), validated
with **two** fresh-context blind runs the way docs/15 defines reviewers to run — read-only,
given only the skill file, the register (@ `2295c64`), their corpus path, doc 06, `CLAUDE.md`,
and regime labels; explicitly barred from `docs/notes/` (the retrospective, stage-gates, all
prior validation notes), `CHANGELOG.md`, `AITODO.md`, the telemetry log, git history, and the
*other* phase's corpus state.

- **Phase 1 — historical-bug regression (the strong form).** The corpus as it stood **before**
  commit `19b2022`'s owner-ruled tier-1 fixes (snapshot of `19b2022~1` = `4903780`, materialized
  into gitignored `scratch/` by the coordinator, not by the reviewer). Those two bugs were real,
  owner-confirmed functional defects that shipped compile-clean and survived a readability
  review — a functional reviewer that cannot find them blind isn't ready.
- **Phase 2 — current corpus (@ `19b2022`).** The fixed corpus: the same two REQs must now trace
  as implemented (regression-positive), and the standing non-fixed findings must still appear.

**Examiner-honesty note:** the expected-findings tables in §1 were drafted and grounded (grep
against both corpus states) *before* either blind report was read. Same caveat as the sibling
(`review-simplicity`) validation: the skill, the register, the expected anchors, and this note
share an authoring session — **blind executor, non-blind examiner**. The S4-precedent stronger
form (owner independently reviews the same corpus and compares) remains available before leaning
on this skill for gate decisions.

## 1. Comparison against expected findings

### Phase 1 — pre-fix corpus @ `4903780` (must-catch regression anchors)

| # | Expected finding (grounded pre-drafting) | Blind run result |
|---|---|---|
| P1-1 | REQ-069 (in-cycle lamp) → **unimplemented**: `DB_Output.In_Cycle := iDB_ShredderSequencer.IO.InCycle` (pre-fix FC_ControlMain), and grep finds **zero writers** of `IO.InCycle` corpus-wide — the lamp can never light | **Found**, exact evidence (both consuming COILs quoted + corpus-wide zero-writer grep + "none of the FB's 15 networks writes it"), and extended: three of the ruling's five feedback sources aren't even wired toward the lamp, and the register's citation of the ruling comment doesn't exist in this snapshot (raised as NEW N-6 — honest anachronism catch) |
| P1-2 | REQ-012 (stop pushbutton) → **contradicted**: tag comment "(NC)" vs the non-negated map line (`… AND DI4_SYS_CycleStop …`, pre-fix FC_Inputs N1) → stop is asserted whenever the healthy NC button is unpressed; the plant can never start | **Found**, exact mechanism ("pressing stop *releases* it"), traced through to REQ-001 → unimplemented (start transition permanently blocked), and extended: the stop path never reaches the pusher subsystem at all (NEW N-3) |

### Both phases — standing expectations

| # | Expected | Phase 1 | Phase 2 |
|---|---|---|---|
| B-1 | Overcurrent REQs (REQ-017/021/022; the REQ-023…025 reaction chain's trigger) → **disarmed/unimplemented, never "implemented"**: FB_ShredderSequencer N12 gates both detection timers `AND NOT AlwaysTrue`; `DB_Settings.OvercurrentSetpointMedium/High` consumed nowhere; step 60 reachable only via the disarmed trip | **Found**, sharper than expected: the whole 11-REQ overcurrent group (017, 021–029, **plus REQ-058's annunciation**) verdicted disarmed with both gates quoted, dead setpoints + dead current member confirmed, step-60 unreachability derived — and the numbers along the disarmed chain still verified (4.0/180.0/5/3.0) | **Found** — same 11-REQ disarmed group, same evidence, none called implemented |
| B-2 | `DB_Input.Pusher_Local_Remote` **written-but-never-consumed** (dead input). The register has no selector REQ (Q-03 carries it) — expected disposition: unrequested/dead input, question carried not answered | **Found** (REQ-036 unimplemented + dead-member listing, Q-03 carried) | **Found** ("written … and read by **nothing**"; dead wiring in Pass 2; anti-laundering explicitly clean) |
| B-3 | Reverse-pass justified-extras set traced or flagged: pusher cold-start pump guard (`PumpEverDemanded`, FB_PusherControl N13), pressure-trip counting + blocked threshold (pusher N4/N5 → REQ-046/048), jog pre-start warning (pusher N10/N11 → REQ-041), sequencer reversal re-arm window (sequencer N3–N5 → REQ-028) — each mapped to its REQ or listed as unrequested with the C-606 justification check | **Found**, all four mapped to their REQs (guard comment quoted; window semantics queued as an S9 candidate) | **Found** — same four dispositions; run-on mechanism credited, its zero-preset consequence flagged ("the pump **does** stop during the 2 s hold") |
| B-4 | Imported-real extras (hours-run counter → REQ-065 binding candidate; HMI telemetry; hand-intervention machinery; `Name : String`) dispositioned as **imported-real context**, not fix demands | **Found**, full extras census as context; `HrsRun` correctly credited to REQ-065 (traced read-only into the imported block) | **Found** — "imported-real capability carried disabled (documented context, not fix demands)", full list incl. never-read `HandReverse`, never-written `Name`; `HrsRun` → REQ-065 |
| B-5 | FC_ControlMain N3's constant-wired motor-FB inputs (`InHand`/`RecentStart`/`InhibitMotor`/`HandReverse`): the register asks for no shredder hand mode → expected: noted constant-wiring/unrequested with a route note (C-604 is the simplicity tier's rule) — **not** an unimplemented-REQ verdict | **Found** with the expected disposition + route note — and one strap correctly promoted to functional: `RecentStart := AlwaysTrue` is load-bearing in REQ-062's contradiction (see beyond-anchor list) | **Found**, same disposition + C-604 route note; the `RecentStart` strap again identified as having "functional teeth (REQ-062)" — independently re-derived |
| B-6 | Timing sweep: the nine unconfigured `DB_Settings` members → **partial** verdicts cross-citing Q-02 (esp. REQ-047's `PressureClearResumeDelay`, unconfigured despite a spec-stated 5 s); configured members verified against spec numbers | **Found**, and materially extended: PT=0 *consequence analysis* per member (discharge confirm aborts the start sequence at step 20; pusher stroke timeouts collapse a cycle into a one-scan 0→10→30→0 churn; resume delay vanishes; pump run-on gap at the hold step) — plus MUL/CONVERT pair-index verification per number | **Found**, same consequence analysis ("live zeros, not inert gaps") rolled into the headline: *the build as committed cannot start* |
| B-7 | Proposed-tag REQs (REQ-018/019/020/036/064/066/067) → unimplemented/partial with the register's `proposed` status honored; **no** logic found referencing a proposed tag (no anti-laundering breach expected in either corpus state) | **Found**; explicit "no pipeline breach" declaration | **Found**; explicit "grep-verified zero hits. No pipeline breach" |
| B-8 | Calibrations honored: mapping-rail `AlwaysTrue` + `Test[]` never called disarmed; open questions carried, never answered; C-113 paradigm check against the register's table (both sequencers stepped, mapping/alarms combinational) | **Honored** on all counts — rail idiom distinguished from N12's gate; all 15 Q's carried with "what the corpus does today"; 6 NEW questions clearly marked; C-113 check done, with the lamp's "mismatch by absence" | **Honored** — rail/`Test[]` skipped per calibration; all 15 Q's carried with the corpus's current answer; 4 NEW questions marked; C-113 check: "No mismatch" (lamp now combinational as classified) |

### Phase 2 only — regression-positive + residue

| # | Expected | Result |
|---|---|---|
| P2-1 | REQ-069 → **implemented**: FC_ControlMain N7 five-feedback OR (`Shredder_Run_Fwd_FB OR Shredder_Run_Rev_FB OR Discharge_Conv_Running OR Infeed_Conv_Running OR Pusher_PowerPack_Running`) with the owner-ruling comment | **Found** — implemented, the exact five-feedback OR quoted, "exactly the five field feedbacks named by the owner ruling, not PLC run commands", all five input hops verified through FC_Inputs |
| P2-2 | REQ-012 → **implemented**: FC_Inputs N1 `NOT DI4_SYS_CycleStop` with the reconciling polarity comment | **Found in substance, sharper verdict** — the fix itself independently confirmed ("polarity absorbed at the map — active-high stop; correct, not the historical stuck-stop polarity"), but the REQ verdicted **partial** because the stop path still never reaches the pusher subsystem — the same gap Phase 1 raised pre-fix, re-found independently post-fix (cross-phase consistency on an unprompted finding). The regression-positive intent (polarity defect seen as fixed) is fully met |
| P2-3 | `iDB_ShredderSequencer.IO.InCycle` now declared-but-unwired: found by the reverse pass, and its documented disposition (N7 comment: superseded; removal deferred to the queued interface rework) honored rather than re-flagged as an unexplained defect | **Found**, disposition honored ("the superseded `IO.InCycle` member is dead but documented") — listed as documented dead structure, not an unexplained defect |

## 2. Verdict

**Validated, both phases.** Every must-catch anchor was independently reproduced with quoted,
grep-grounded evidence:

- **Phase 1 (the strong form)** found both owner-ruled historical bugs blind — the
  consumed-but-never-written in-cycle lamp (REQ-069 unimplemented, with the corpus-wide
  zero-writer grep) and the NC stop polarity (REQ-012 contradicted, with the "pressing stop
  releases it" mechanism) — the two defects that had already survived the compile gate, a
  presentation, and a tier-2 readability review. This is the regression evidence the skill
  exists for.
- **Phase 2** confirmed both fixes as regression-positives (REQ-069 implemented with the ruling's
  five field feedbacks verified hop-by-hop; the DI4 polarity fix explicitly recognized as
  "correct, not the historical stuck-stop polarity") and honored the deferred-removal disposition
  of the superseded interface member.
- **All eight standing expectations (B-1…B-8) were found in both phases**, including the one that
  most tests calibration: the whole overcurrent group held at *disarmed* — never credited as
  implemented despite complete-looking logic — while the mapping-rail `AlwaysTrue` idiom was
  never miscalled. Open questions were carried with "what the corpus does today" answers, never
  resolved; both runs declared the anti-laundering check explicitly clean.

**Beyond-anchor discoveries (upside, both runs, independently consistent):** the
reverse-never-runs structural conflict (the sequencer's 6 s reverse window expires inside the
imported motor FB's 8 s entry-pause — statically proven from the two presets); the as-committed
*cannot-start* consequence analysis of the unconfigured settings (PT = 0 aborts the start
sequence at the discharge-confirm step and collapses every pusher cycle in one scan); the
disabled-pusher contradiction pair (REQ-043/044); the E-stop auto-restart contradiction
(REQ-062, with the `RecentStart := AlwaysTrue` strap identified as load-bearing by both runs);
the unwarned-pusher-motion cluster; and the unconfigured *per-instance* motor windows
(`FTTime` = 0 ⇒ instant failed-to-run trip) that sit outside Q-02's DB_Settings scope. Several
of these are tier-1 defects that the compile gate and the simplicity review had both already
passed over — the stricter-bar disposition doing exactly what docs/15 wants.

**Cross-phase consistency worth recording:** Phase 2 independently re-found Phase 1's
pusher-outside-the-stop-path gap (neither run was prompted toward it) and graded REQ-012
*partial* on that basis post-fix — a sharper verdict than the expected table's "implemented,"
and the correct one. **One vocabulary observation, not a failure:** the two runs labeled the
same reverse-run defect differently (Phase 1 "contradicted — does materially other"; Phase 2
"unimplemented — chain broken, behavior can never occur") and similarly split on REQ-006. The
substance and evidence agreed exactly; the `contradicted`/`unimplemented` boundary in the
skill's verdict vocabulary could take one sharpening sentence in a future revision. Verdict
totals reconcile: Phase 1 26/16/8/11/7/1, Phase 2 28/18/11/7/4/1 (impl/partial/…), both sum 69,
and every movement is explained by the two fixes plus that vocabulary split. Phase 1's NEW N-6
(the register cites the REQ-069 ruling comment that doesn't exist in the pre-fix snapshot) is an
examiner-explained artifact of reviewing a historical snapshot against the current register —
the citation is correct at HEAD; no register action needed.

**Method caveat, stated honestly:** blind executors, non-blind examiner — the skill, the
register, the expected-findings anchors, and this comparison were authored in the same session
that graded the runs. The S4-precedent stronger form (owner independently reviews the same
corpus) remains available before this skill gates anything alone.

**Cost note:** Phase 1 ran ~24 min / ~197k tokens. Phase 2 was interrupted once by a transient
server error (529) and resumed from its transcript; combined estimate ~250k tokens across both
attempts. The phases ran concurrently (~45 min wall for the whole exercise) — roughly double the
sibling's single-run cost, as planned for the two-phase design; scope to Phase-2-only for
routine gate use, at the cost of the regression evidence.

**Owner follow-ups surfaced by the runs** (consolidated; the reports carry the detail): the
Q-02/NEW-1 zeros must be configured before any live use (the build as committed cannot start);
rule on the pusher's exemption from stop-all (NEW-2/N-3); rule on the reverse-run ownership
conflict (NEW-3/N-2 — sequencer window vs motor-FB entry-pause); rule on unwarned pusher motion
and jog-while-blocked (NEW-4/N-4/N-5); fix or re-rule the disabled-pusher contradictions
(REQ-043/044); give the sequencer a healthy input and retire the `RecentStart` strap or rule it
intentional (REQ-062); decide the E-stop/power-cycle restart posture (Q-01).

## 3. Blind run reports (verbatim) — moved to docs/evidence/

The full verbatim blind-run reports (~850 lines: the fresh-context
agent's unedited output, including the raw `converter review` mechanical dump) is
preserved at `docs/evidence/review-functional-blind-run-2026-07-16.md`, split out 2026-07-18 (FI-19) to keep this note
focused on the §1/§2 comparison and verdict. It is kept there verbatim and unedited —
the property that makes it usable as drift-check and blindness evidence.
