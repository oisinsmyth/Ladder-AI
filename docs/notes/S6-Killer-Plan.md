# S6-Killer-Plan — PlantAutoControl answer-key validation

A plan to turn JOB9002's `PlantAutoControl` block + its dependency graph into a **ground-truth-graded
pipeline test**: derive an intent-level spec from the real block, generate blindly from that spec
through the normal pipeline, then grade the generated blocks against the real block as an answer
key — with the grading built to treat *divergence toward cleaner/safer/more-compliant logic as a
win*, not a miss.

**What this is:** a validation / benchmark fixture that gives the project the ground-truth accuracy
measurement it currently lacks (`FI-16` telemetry measures tokens/latency; the reviewers measure
compliance against a register *we* wrote — nothing measures "did the pipeline reproduce a known-correct
real implementation"). It also attacks target scarcity (`FI-30`: `test-project001` is fully
implemented; obvious asks get rejected by reuse-first). **What this is not:** an S6-exit generation
request (those are ten *fresh plain-language* owner requests). It is closer in spirit to `FI-05`
(blind-proof) and `FI-01` (testing hook). Pursue it for what it is — don't let the S6 tally muddy it.

## Roles (each a distinct `lad-coder` dispatch; the answer key is handed only to roles allowed to see it)

- **Examiner** — reads real `PlantAutoControl` + deps, writes the spec. **Sees** the answer key.
- **Candidate** — generates from the spec. **Never** sees the answer key; only the frozen spec artifact.
- **Grader** — compares generated blocks against the answer key. **Sees** both.

Separation is enforced by the orchestrator controlling what each dispatch receives. The one
contamination risk is the orchestrator carrying answer-key knowledge into how the Candidate is
briefed — mitigation: the spec is frozen to a committed artifact, and the Candidate dispatch is
briefed **only** with that artifact, no side commentary.

All reads/writes/compiles of IR go through `lad-coder` (hard rule 8); the orchestrator plans,
dispatches, and verifies evidence.

## Phase 0 — Setup, boundary, and sealing the answer key

1. **Data boundary first.** `JOB9002` is Amber-tier. Either record a per-project approval in
   `docs/13-data-boundary.md` scoped to "PlantAutoControl + dependency graph, validation use," **or** run
   `converter sanitize` up front (preferred — keeps everything downstream Green and removes ongoing
   boundary friction).
2. **Inventory the dependency graph** (Examiner, via `reuse-scan` / `digest` / `cross-check`): every
   block `PlantAutoControl` calls or shares storage with. Classify each:
   - **Regenerate** — the target set (the "7/8").
   - **Assume-available** — standard library / instruction-level deps (a `TON` is an instruction, not
     a block; a called library FB is a boundary, not a regeneration target).
   - **EXCLUDE + STOP** — any F-block / safety content (hard rule 2). Walled off and flagged, never
     read or regenerated.
3. **Seal the answer key.** Snapshot real `PlantAutoControl` + the regenerate set as committed IR under a
   sealed path (e.g. `docs/evidence/PlantAutoControl-answerkey/`). Frozen, never edited — the ground truth
   the Grader diffs against.

## Phase 1 — Spec derivation (Examiner)

Produce the **`requirements.md` REQ register only** — the intent-level WHAT, shaped like the
`gen-spec-analysis` register (the test-project001 register is the canonical format). **Not
`architecture.md`:** the design (block manifest, interfaces, wiring) is the Candidate's own
`gen-architecture` output in Phase 2 — deriving it here would leak the block breakdown (the very
"7/8 blocks" structure the pipeline should discover) and skip testing the Design stage. The full
step-by-step Examiner protocol (reverse-derive path, Level-1 scope, ban-list + leakage audit) is
planned separately.

**Anti-leakage discipline — the make-or-break rule.** The spec carries *intent*, not *implementation*:

- **Allowed** (genuine requirements): process behavior and interlocks, quantitative process
  parameters (a 5 s dwell, an overcurrent setpoint — requirements exactly like Q-04's setpoints),
  operator/HMI-facing behavior, fault responses.
- **Banned**: specific instructions ("use a TON"), network numbers, rung structure, tag-level wiring,
  coil/contact choices, edge-memory mechanics. "Network 3 gates the run coil" is leakage — reword as
  the behavioral requirement it encodes.
- **Leakage checklist** over the finished spec before freezing: grep for instruction mnemonics,
  network refs, IR idioms; each hit justifies itself as a process requirement or is reworded.
- **Record every abstraction judgment** ("requirement or implementation detail?"). Those judgment
  points are the likeliest spec-gaps and pre-seed Phase 3's SPEC-GAP calls.

Then **freeze** the spec (commit). The answer key is set aside; the spec must stand on its own.

## Phase 2 — Blind generation (Candidate)

Fresh `lad-coder`, spec-only, runs the normal pipeline unchanged: `gen-architecture` →
`gen-block-new` per block → `preflight` (zero findings) → convert → import → compile gate → the three
reviewer skills. Output is the standard presentation bundle: generated IR + intent + compile evidence
+ reviewer findings. **No comparison to `PlantAutoControl` happens here** — the Candidate doesn't know it's
reproducing a real block.

## Phase 3 — Grading with the "should be better" allowance (Grader)

Functional, not byte-identical. `converter diff` / `cross-check` / `trace` inform it, but the verdict
is behavioral. Each REQ and each behavior in the answer key gets one verdict:

| Verdict | Meaning | Counts as |
|---|---|---|
| **MATCH** | Functionally equivalent (structure may differ freely) | PASS |
| **IMPROVEMENT** | Equivalent-or-safer function **and** cleaner/more compliant than `PlantAutoControl`; `PlantAutoControl`'s version was the site compromise | **PASS — and a win** |
| **DEFECT** | Behaves differently from the *process intent* in a wrong way (missing interlock, wrong polarity, unimplemented REQ) | FAIL |
| **REGRESSION** | Dropped a **load-bearing** behavior `PlantAutoControl` actually needed | FAIL |
| **SPEC-GAP / RFI** | Divergence traces to the spec being ambiguous or silent, not to generation | Not charged to generation; feeds spec/pipeline |

**The direction-of-divergence rule (the allowance).** When generated ≠ answer key, the default is
**not** "generated is wrong." The Grader asks: *which one is correct against the process intent and the
generated-code quality bar?* `PlantAutoControl` is site code held to a **lower** bar (it doesn't even meet
C-001) — so it is a **functional** oracle, never a **style/conventions** oracle. `PlantAutoControl` only
"wins" a divergence when its behavior encodes a real requirement the generated code dropped (→
REGRESSION). If the generated code diverges toward cleaner, safer, or convention-compliant **and loses
no required function**, generated wins (→ IMPROVEMENT).

**Guardrails so "improvement" can't launder a defect:**
- Every **IMPROVEMENT** cites a specific rule ID (C-xxx) or a concrete safety/readability argument,
  **and** asserts functional-equivalence-or-superset with quoted evidence from both sides. No hand-wave.
- **DEFECT** and **REGRESSION** are hard fails regardless of how clean the code looks.
- **"Load-bearing" test** (separates IMPROVEMENT from REGRESSION): a dropped behavior is load-bearing
  if, for some reachable process/fault condition, removing it violates the process intent. If
  `PlantAutoControl` did X only as a legacy/site workaround or redundant belt-and-suspenders the intent
  doesn't require, dropping X is IMPROVEMENT; if X handled a real fault/edge case, dropping it is
  REGRESSION.
- **The owner is final arbiter** on genuine IMPROVEMENT-vs-REGRESSION judgment calls (did
  `PlantAutoControl`'s quirk actually matter?). Mirrors the real review gate — the tool proposes, the
  engineer rules.

## Phase 4 — Scorecard & persistence

- Per-block: REQ coverage + counts of MATCH / IMPROVEMENT / DEFECT / REGRESSION / SPEC-GAP.
- Headline "N/8 passed" = blocks with **zero DEFECT/REGRESSION** on all required behavior (MATCH or
  justified IMPROVEMENT throughout).
- Three finding streams, each actionable: **DEFECTs** → pipeline bugs; **SPEC-GAPs** → spec/RFI signal
  (and evidence about the honest abstraction level a real register needs); **IMPROVEMENTs** → concrete
  proof the stricter generated-code bar is real and the pipeline clears it.
- Persist per the `docs/evidence/` convention (`FI-19`): verbatim grading transcript to
  `docs/evidence/`, lean summary + link in a note. This is a reusable validation fixture, so it should
  be born in that shape.

## Net

Blindness is structural (three separated roles, sealed key). The spec is forced to intent-level with
a leakage checklist. Grading treats a clean, safer, more-compliant divergence as a **win against** the
answer sheet rather than a miss — guarded by rule-ID citation, the load-bearing REGRESSION test, and
owner arbitration so the allowance can't be abused to excuse a real defect.
