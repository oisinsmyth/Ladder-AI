# 02 — Staged Scope Expansion (Roadmap)

Stages follow the project goals in priority order. Each stage has entry criteria (what must already be true), deliverables, and exit criteria (definition of done). A stage does not start until the previous stage's exit criteria are met — read capabilities are proven before write capabilities are attempted.

Two cross-cutting rules apply from the first write-capable stage (S3) onward:

- **Compile gate (Goal 7):** nothing is presented as complete until it imports and compiles cleanly in TIA Portal.
- **Safety exclusion (Goal 11):** permanent, applies to every stage. The pipeline must *refuse* to export, convert, or touch F-blocks, not merely avoid them.

## S0 — Foundation

*Entry:* TIA Portal V20 + Openness installed; user in the "Siemens TIA Openness" Windows group.
*Deliverables:* Repo skeleton per `05-architecture.md`; `docs/notes/stage-gates.md` created with S0 marked active (referenced by `CLAUDE.md` — must exist from day one); Openness CLI that attaches to Portal, opens a project, lists blocks — with the safety filter present from the first read-only command: `list` identifies and flags F-/safety blocks and never opens or exports safety content (Goal 11 is never retrofitted); reference TIA project with representative LAD (timers, counters, edges, comparisons, moves, block calls, parallel branches, DB and UDT access).
*Exit:* One command lists all blocks of the reference project, with F-blocks flagged, not opened. First-connect approval dialog documented in `docs/notes/`. Assumption log items A-01/A-02 verification results recorded in `docs/notes/`.

## S1 — Lossless round-trip (Goal 1)

*Entry:* S0 done.
*Deliverables:* Export of LAD blocks + tag tables + UDTs + DBs to SimaticML; SimaticML↔IR converters; golden-file round-trip test suite (`08-testing-strategy.md`).
*Exit:* Every block in the reference project survives export→IR→SimaticML→import→compile→re-export with no semantic diff. Known-benign diffs (volatile IDs, ordering) documented and normalized.

## S2 — Read and explain (Goal 2)

*Entry:* S1 done — the IR is trustworthy.
*Deliverables:* Claude Code reads IR and produces plain-language explanations of networks and blocks; explanation quality checklist.
*Exit:* Explanations of 10 sampled networks judged accurate by the engineer; no hallucinated tags or behavior.

## S3 — Comment generation (Goal 3) — first write path

Comments are the lowest-risk write: they cannot change logic. This is where the compile gate, import path, and review workflow get proven.
*Entry:* S2 done; `11-review-workflow.md` agreed.
*Deliverables:* AI writes network titles/comments and block comments into the IR; converter carries them into SimaticML; import+compile verified automatically.
*Exit:* An undocumented block gets useful comments end-to-end: generated, imported, compiled, human-approved.

## S4 — Convention review (Goal 4)

*Entry:* `06-lad-conventions.md` populated with real workplace rules (blocker — the conventions must be written first).
*Deliverables:* Review mode: AI checks IR against the conventions and emits a findings report (rule ID, location, severity, suggested fix). No auto-fix yet.
*Exit:* Review of the reference project matches the engineer's own review on a sample; false-positive rate acceptable.

## S5 — Structured data extraction (Goal 5)

*Entry:* S1 done (S4 not required — can run in parallel with S3/S4).
*Deliverables:* Extractors for alarm lists, IO usage, and cross-references, emitting CSV/XLSX; these also feed existing manual work (HMI alarm tables, O&M manuals, IO checklists).
*Exit:* Extracted alarm and IO lists for the reference project verified against TIA's own cross-reference data.

## S6 — Generation from plain language (Goals 6 + 7)

*Entry:* S3 done (write path proven); pattern spec `07-pattern-library-spec.md` implemented with a seed library (~10 patterns: motor start/stop, valve control, debounce, alarm latch, pulse, sequence step, etc.).
*Deliverables:* AI composes new networks/blocks from the pattern library using real exported tags; automatic import+compile loop — AI iterates until clean compile before presenting anything.
*Exit:* Ten plain-language requests produce compiling, human-approved LAD with zero invented tags. Freeform (non-pattern) rungs require explicit human opt-in per request.

## S7 — Modify existing networks (Goal 8)

Highest-risk capability: changes to existing logic.
*Entry:* S6 done; diff tooling shows exactly which networks changed and proves the rest are identical in IR.
*Deliverables:* Targeted network edit mode — AI changes only the named network(s); surrounding-logic invariance check; before/after diff for review.
*Exit:* Ten modification tasks with zero unintended changes outside the target networks.

## S8 — Pattern library maturation (Goal 9)

*Entry:* S6 in use.
*Deliverables:* Grow the library from real approved generations; each pattern gains tests and usage docs per the spec; generation measured by pattern coverage (share of requests satisfiable without freeform rungs).
*Exit:* Ongoing — reviewed at each stage gate.

## S9 — Test and simulation generation (Goal 10)

*Entry:* S6/S7 in use; PLCSIM story resolved (risk R-07 — confirm whether PLCSIM Advanced supports S7-1200 G2; fallbacks: TIA-integrated PLCSIM, or an S7-1500 shadow project for logic-level testing).
*Deliverables:* AI generates test sequences (stimulus + expected response per scan) for generated/modified logic; harness drives the simulator and reports pass/fail.
*Exit:* Every S6/S7 deliverable ships with at least one passing simulated test.

## Sequencing rationale

Read before annotate, annotate before review, review before generate, generate before modify, everything before simulate-and-verify. Each stage builds the trust and infrastructure the next depends on. Goal 7 (compile gate) and Goal 11 (safety exclusion) are invariants, not stages.
