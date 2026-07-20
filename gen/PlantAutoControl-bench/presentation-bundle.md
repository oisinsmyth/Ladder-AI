# PlantAutoControl — Phase 2 presentation bundle (S6-Killer-Plan)

The blind-generation output for the S6-Killer-Plan answer-key validation
(`docs/notes/S6-Killer-Plan.md`). A blind Candidate regenerated `PlantAutoControl` from the frozen
register through the normal pipeline; this bundle is the standard presentation **and Phase 3's
grading input** (the Grader compares it to the sealed answer key at `docs/evidence/PlantAutoControl-answerkey/`).

## Integrity notes (Phase 3 must weight these)

- **Gate-1 sign-off is answer-key-biased.** Owner chose *run straight through*; the orchestrator (who
  had seen the answer key) approved gate 1, not an independent engineer. Recorded in `architecture.md`.
- **Weaker blindness via patterns.** The Candidate was blind to the sealed answer key (never opened
  `docs/evidence/PlantAutoControl-answerkey/`), but `patterns/chained-permissive-enable` was generalized
  *from* the target and embeds four real example networks (2, 7, 8, 12) with coil formulas. Using it is
  the intended pipeline (compose from proven patterns) — so this measures **"patterns + intent →
  correct code," not "invent from nothing."** The Candidate declared this itself.
- **Blindness that held:** three fresh reviewers + the Candidate, each a cold dispatch, none pointed at
  the answer key; the answer-key block was deleted from the compile scratch before coding.

## The generated block

- **`gen/PlantAutoControl-bench/PlantAutoControl.ir`** — new FC, **20 networks**, one automatic-control
  network per plant equipment, in material-train order.
- **Intent:** the per-equipment automatic-control layer (REQ-001…022): each network maps field
  feedback into the equipment interface, computes the auto start permissive (`PlantControl.Status`
  running-state + upstream neighbour `UPSEnable` or a sub-system enable) and the staged shutdown
  request (held until the named neighbour's `ShutdownComplete`, suppressed under hand intervention),
  drives the physical run output, and `CALL`s the equipment FB. Combinational (C-113 "no"); the only
  memory is the REQ-015 auto-pre-start one-shot. Composed from `chained-permissive-enable` (tier b) +
  tier-(a) `CALL`s to the 8 given FB types; net 17 carries ~5% freeform fwd/rev logic.

## Compile evidence (the gate — hard rule 4)

- Preflight (`--project ir/PlantAutoControl-bench/`): **only the accepted C-003 name warning**; zero
  convert/tag/call/instanceof findings.
- Compile on `SampleProject` (independently re-verified by the orchestrator): `PlantAutoControl (FC100):
  Block was successfully compiled` — **ERRORS: 0, WARNINGS: 1** (the warning is a bare-scratch
  hardware-config artifact, not a block defect). Evidence: `gen/PlantAutoControl-bench/compile-evidence.txt`.
- Leak-gated: zero real identifying names in code.

## Reviewer findings (three fresh, blind reviews)

| Review | Verdict | Headline |
|---|---|---|
| **Functional** (`…-review-functional.md`) | **Implements the spec** | 18/22 fully implemented, **0 contradicted / 0 disarmed / 0 unimplemented**, no gold-plating; 3 partials |
| **Conventions** (`…-review-conventions.md`) | Does not clear the strict bar as-is | 2 error-tensions (C-308, C-403) + C-003 naming |
| **Simplicity** (`…-review-simplicity.md`) | Clears conditionally | 4 findings (3 warn/1 info), mostly pattern-inherited density |

## Open items for the engineer (the real decisions)

1. **NEW-01 — rename `DiscreteInputs.CycloneDustAutoRunning/Stop`** (a `/` in the member name, C-005,
   un-referenceable in IR). This is a **given-boundary defect**, not a generation fault — it blocks
   wiring the cyclone running feedback (REQ-019 partial, net 19). The block correctly documented the gap.
2. **REQ-012 — discharge-VSD rotation-sensor bypass clause missing** (net 6): the "unless bypassed"
   term is absent; `BypassAirStarDCRotSen` unreferenced. The one genuine block-authored functional gap.
3. **C-308 — fan-time scan-copy** (nets 3/4/19): `NormalFanStartTime` cyclically MOVEd onto each
   filter's per-instance `EnableUPSTime`. Faithful to REQ-020, but violates the C-308 one-writer rule
   under the stricter bar. Owner ruling: accept (matches site intent) or restructure.
4. **C-403 / Q-11 — startup-reset for `AutoPreStart`** (net 2): no OB100/init block. Ruled out of
   scope for this regeneration (project-level block); surfaced as a known gap.
5. **REQ-016 — feed-conveyor reverse paradigm** (net 17): register classifies it latched; the block
   implements direction combinationally and delegates the hold to `MotorFwdRevSystem`. Direction is
   correct; the latch/hold is an S9 sim-verification candidate (NEW-03).
6. **Pattern-level tensions** (simplicity F1/F2): the `chained-permissive-enable` shutdown-coil boolean
   density and bare `SystemHealthy := TRUE` are inherited from the admitted pattern — ruling them means
   amending the pattern (touches every future 20-network generation).

## Phase-3 pre-seed (candidate verdicts, for the Grader)

- **IMPROVEMENT candidates:** Q-07 per-machine `HandIntervention` on nets 10/11 (the block implements
  REQ-006 correctly; the answer key has a probable copy/paste quirk — cite REQ-006). Net-2 factors the
  JOB9001 interlock out — a cleaner rewrite than the pattern's own example (simplicity reviewer).
- **Tensions to grade against the answer key:** C-308 fan scan-copy (does the answer key do the same? →
  likely MATCH, not a generation DEFECT); REQ-012 bypass (a real DEFECT vs the answer key if it wires
  the bypass); REQ-016 paradigm (MATCH-or-IMPROVEMENT depending on the FB's internal latch).

## Gate 2

Presentation approved by the orchestrator (biased, run-through) as Phase 2's completion. The block is
a compile-clean, spec-implementing proposal with a bounded set of engineer decisions above. **Phase 3
(the Grader) compares this against the sealed answer key** to assign MATCH / IMPROVEMENT / DEFECT /
REGRESSION / SPEC-GAP per REQ.
