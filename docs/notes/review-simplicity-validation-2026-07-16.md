# review-simplicity skill — blind validation against the test-project001 corpus (2026-07-16)

**What this is.** Step 2 of `docs/15-generation-pipeline.md`'s build order: the
`.claude/skills/review-simplicity/SKILL.md` reviewer, validated the way docs/15 defines reviewers
to run — a **fresh-context subagent** given only the skill file, the binding docs, and
`ir/test-project001/` (all 21 files), explicitly barred from reading
`docs/notes/test-project001-retrospective.md` (the expected-findings anchor). The agent had no
authoring context and no conversation history about this code. Its full report is preserved
verbatim in §3; §1 is the comparison against the known findings, §2 the verdict.

## 1. Comparison against the retrospective's known findings

| Known finding (retrospective) | Blind run result |
|---|---|
| F-1 duplicated cycle-start compound (C-601, Pusher N4/N6) | **Found**, exact evidence, near-match diff highlighted |
| F-2 bare interface UDT members (C-605) | **Found** (30/30 and 23/23), with the honest format caveat the skill mandates |
| F-3/§5.1 HMI-Times batch + pairing comment condition | **Correctly calibrated**: generated FBs clean (comment present), real block flagged as context (comment absent), pair-4 name-crossing caught |
| F-5/§5.2 settings-access split (C-607) | **Found**, sharper than the original: three-way table showing the *real* block is the C-307-compliant one and each generated FB deviates differently |
| F-6 ranged step predicates (C-603) | **Found**, all three |
| F-8 bare `AlwaysTrue` constants (C-604, ControlMain N3) | **Found**, plus two aggravators the retrospective missed (below) |
| F-10 "Output Mapping" mistitle | **Found** (C-203/one-reading) |
| F-11 `Snake_Case` buffer naming | **Found** (C-607/C-001) |
| F-12 startup machinery | Correctly routed to "Not checkable here" (out of tier-2 scope per the skill) |
| Dead `Pusher_Local_Remote` input | **Found**, plus a second dead input (below) |
| Overcurrent placeholder handling | **Found**, and extended: the "see member comment" pointers are dangling, and both `DB_Settings` setpoints are dead corpus-wide |
| Real-block context set (§3.8) | **Found** and extended (telemetry mega-EN, S/R web, TONR, untitled N5, plus suspected real defects below) |

Missed vs the retrospective: only F-4 (Pusher N4 multi-function packing — which the retrospective
itself marked "noted, not pressed"). Nothing material was missed.

**New genuine discoveries the retrospective did not contain** (all grep-verified by the agent):

1. **`IO.InCycle` is consumed into physical output `DQ8_SYS_InCycle` but written nowhere** — the
   in-cycle lamp is permanently off. Tier-1 (functional) candidate.
2. **`DI4_SYS_CycleStop` is commented "(NC)" in the tag table but mapped non-negated** — as wired,
   an NC stop button would hold `StopCmd` permanently true (plant can never start). Either the
   polarity or the tag comment is wrong. Tier-1 candidate; the input-mapping pattern's negated
   variant exists for exactly this case.
3. **`RecentStart := AlwaysTrue` (ControlMain N3) fights the motor FB's own management of that
   bit** — FB network 4 documents an HMI set-bit contract and clears the bit itself; the cyclic
   constant re-set makes that logic decorative.
4. `HandReverse` is wired to a member **no FB network reads** (declaration only).
5. `DB_Input.Infeed_Conv_Running` is mapped and never consumed (second dead input).
6. **The admitted `motor-dol` pattern's own example breaches C-126's new exception condition**
   (its "HMI Times" network has no scheme comment; also an untitled network) — flagged as a
   pattern-vs-rule tension for owner ruling, exactly per the skill's calibration rule.
7. Suspected defects **in the imported-real block**, labeled context + verify-against-TIA, not
   fix demands: `HandPosEdge` written twice (second overwrites first) while twin `HandNegEdge` is
   never referenced (reads like a miswire); a dead `AND Pasue … AND NOT Pasue` annihilated term;
   N13's hours-counter edge guard reads its own edge memory after same-network update (guard
   plausibly always false); duplicated `NOT HandPosEdge AND NOT HandPosEdge`.
8. Tag table: 58 of 98 entries are uncommented legacy noise unreferenced by any logic.
9. Sequencer N6 carries a redundant `IO.DownstreamRunning` term already implied by `NOT StopCmd`.
10. ControlMain N1's sounder cross-wire is a real, uncommented one-scan lag (sequencer N11's own
    comment is the house-style template for it).

## 2. Verdict

**Validated.** The blind run independently reproduced every material known finding, applied the
calibration rules correctly (mapping rail skipped, C-121 verbosity skipped, real-block regime
respected, pattern tensions flagged not adjudicated), followed the report format, and went
meaningfully beyond the anchor — including two tier-1 functional candidates that a tier-2
readability walk surfaced exactly the way the one-reading test predicts. Per the stricter-bar
principle this is the disposition wanted: err toward flagging, judgment findings labeled as such.

Method note, stated honestly: the expected-findings anchor and the skill were written by the same
session that ran this validation, so this is "blind executor, non-blind examiner." The
S4-precedent stronger form (owner independently reviews the same corpus and compares) remains
available if wanted before leaning on the skill for gate decisions.

Cost note: the run took ~20 minutes and ~200k tokens for the full 21-file corpus — appropriate
for a gate review; scope per-block for quick checks.

**Owner follow-ups surfaced by this run:** verify/fix InCycle and DI4 polarity (tier-1
candidates); rule on the `motor-dol` pattern-example tension (add the scheme comment + title to
the pattern, or waive); decide whether the imported block's suspected defects get verified
against the TIA original (converter-fidelity check, hard rule 7 — no hand-patching).

## 3. Blind run report (verbatim) — moved to docs/evidence/

The full verbatim blind-run report (~207 lines: the fresh-context
agent's unedited output) is preserved at `docs/evidence/review-simplicity-blind-run-2026-07-16.md`, split out
2026-07-18 (FI-19) to keep this note focused on the §1/§2 comparison and verdict. It is
kept there verbatim and unedited — the property that makes it usable as drift-check and
blindness evidence.
