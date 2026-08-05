# S6-Killer-Plan — Scorecard (Phase 4)

Ground-truth accuracy result for the PlantAutoControl answer-key validation
(`docs/notes/S6-Killer-Plan.md`). The blind pipeline regenerated `PlantAutoControl` (20-network
orchestration FC) from a reverse-derived spec; the Grader compared it, functionally, against the
sealed answer key with the "should be better" allowance. Full evidence (both-sides quoted IR per
verdict): `docs/evidence/PlantAutoControl-bench-grading.md`. Independently diff-corroborated on the key
verdicts (REQ-003 / REQ-006 / REQ-012).

## Headline

**FAIL the "MATCH-or-better on all required behavior" bar** — the generated block is not free of
DEFECT/REGRESSION. It earns **1 confirmed, safety-relevant IMPROVEMENT**. This is a *good, honest
result*, not a disappointing one: the grade is credible precisely because the Grader found real
gaps rather than rubber-stamping a pipeline's own output.

## Scorecard (22 REQs; recommended verdicts, owner-contingent items marked)

| Verdict | Count | REQs |
|---|---|---|
| MATCH | 14 | 001, 002, 004, 005, 007, 008, 009, 010, 013, 015, 018, 020, 021, 022 |
| IMPROVEMENT | 1 | 006 *(confirm Q-07)* |
| DEFECT | 3 | 011 *(partial, reverse feedback)*, 012 *(firm — bypass clause)*, 017 *(fault interlock narrowed)* |
| REGRESSION | 1 | 003 *(filter cascade hold — owner-contingent A)* |
| SPEC-GAP / OWNER RULING | 3 | 014 *(JOB9001 semantics, Q-03)*, 016 *(reverse latch, rec. REGRESSION)*, 019 *(filter feedback pairing, rec. DEFECT)* |

## The three finding streams

- **DEFECTs → pipeline-bug signal.** REQ-012 (firm): the discharge-VSD rotation-sensor **bypass
  clause** was dropped — wired unconditionally, `BypassAirStarDCRotSen` unreferenced. REQ-011 /
  REQ-017: reverse motion-confirmation and the sorter `FaultActive` interlock were **weakened**. The
  common thread: on freeform interlock detail the pipeline **under-constrains** (drops a guard the
  answer key had), it does not over-constrain.
- **REGRESSION → the same edge.** REQ-003: the cascaded-shutdown neighbour-`ShutdownComplete` hold was
  dropped on all three filter units (kept only the global `FansShutdownReady`). Concrete risk: dust
  filtration stops while its dust generator still runs — unless `FansShutdownReady` provably subsumes
  the neighbour term (owner ruling A).
- **SPEC-GAPs → the register's honest abstraction gaps.** REQ-014 (JOB9001 text-vs-table tension, Q-03),
  REQ-016 (reverse paradigm), REQ-019 (filter feedback pairing + the `/`-in-tag boundary defect,
  NEW-01), REQ-017 net-11 comms words (Q-06). These are exactly the places the reverse-derived
  register was inferred/ambiguous — spec/RFI signal, not generation bugs.
- **IMPROVEMENT → the stricter generated-code bar is real.** REQ-006: the generated block gates each
  machine's auto-shutdown on its **own** `HandIntervention`; the answer key used a *neighbour's* flag
  (a real site copy/paste quirk, register Q-07). The "should be better" allowance credited it —
  functionally safer, no function lost, cited to REQ-006. (Also, structurally: the simplicity reviewer
  found net-2 factors the JOB9001 interlock out more cleanly than the pattern's own example.)

## Owner-arbitration items (the Grader recommends; the owner rules)

A) REQ-003 filter cascade → REGRESSION unless `FansShutdownReady` subsumes the neighbour term.
B) REQ-014 incline-feed JOB9001 → SPEC-GAP (register text vs table, Q-03).
C) REQ-016 feed-reverse latch → REGRESSION unless `MotorFwdRevSystem` guards `Reverse` internally (NEW-03 sim).
D) REQ-017 sorter fault → DEFECT unless `FaultActive ≡ ComFlt` in the FB.
E) REQ-019 filter feedback pairing → DEFECT if `Op` is the true running signal, else SPEC-GAP.
F) REQ-006 own-hand fix → IMPROVEMENT unless grouped-hand is declared intentional.

## What this measures (integrity-weighted)

Two caveats weight the read (recorded since Phase 2): gate-1 sign-off was **answer-key-biased**
(orchestrator-approved, owner's run-through choice), and blindness was **weaker on pattern-shaped
networks** (`chained-permissive-enable` was generalized from this target). Accordingly the MATCHes on
pattern-shaped machines (nets 2/7/8/12 + the starter family) are **low-information** — the pipeline
reproduced what the pattern already encoded. **Every DEFECT, the REGRESSION, the IMPROVEMENT, and all
three owner-rulings landed on the freeform / decision networks** (1, 3/4, 6, 10, 11, 17, 19), where
pattern support was thin.

**The honest measured signal:** composition-from-patterns is strong; **freeform interlock
reconstruction is the exposed edge**, and it errs toward **under-constraining** (dropping a guard)
rather than over-constraining — while also correctly **fixing** one real site defect. That is a
precise, actionable accuracy measurement the project did not have before — and a concrete pipeline
improvement target: strengthen freeform interlock synthesis (cascade-hold completeness, bypass/latch
preservation) so it stops shedding guards the spec implies.

## Fixture

The whole chain — sealed answer key, given boundary, reverse-derived register, blind architecture +
generated block + compile evidence, three reviews, and this grading — is committed under
`gen/PlantAutoControl-bench/`, `ir/PlantAutoControl-bench/`, and `docs/evidence/`. It is reusable: re-runnable
as the pipeline improves, and a template for grading future blocks against real answer keys.
