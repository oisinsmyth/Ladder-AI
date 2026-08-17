# 02 — Staged Scope Expansion (Roadmap)

> # 🛑 SUSPENDED — 2026-08-17
>
> **The staged development plan is suspended (time constraints + real application needs).** The stage
> progression below does not advance: no stage opens, closes, or has its gate reviewed while the
> suspension stands. Entry/exit criteria are **frozen as written, not waived** — in particular S6's
> exit criterion (ten fresh requests, at 1) and S7's `S6 done` entry gate are unchanged and unmet.
>
> **Canonical notice, including what is *not* suspended (the hard rules, the data boundary, the built
> tooling, and live-job work): `03-development-plan.md`.** Current per-stage status:
> `docs/notes/stage-gates.md`. Everything below is retained verbatim as the plan of record.

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

*Rulings (recorded here 2026-08-05; the decisions themselves are the project owner's, evidence in `docs/evidence/stage-S6.md`):*
- **Entry — the "~10 patterns" seed target is approximate, and was satisfied at S6 entry by two well-proven pattern *kinds*** (equipment-instance FB/FC via `CALL`; documented repeated rung-shape), not by a literal count of ten. The library grows organically once S6 is running (owner's call, 2026-07-15).
- **Exit — the ten are ten *fresh* plain-language generation requests.** Fix waves are tracked separately and do not count toward the ten, and neither do validation fixtures (ruling D-4, 2026-07-18). The live tally is kept in `AITODO.md`.

## S7 — Modify existing networks (Goal 8)

Highest-risk capability: changes to existing logic.
*Entry:* S6 done; diff tooling shows exactly which networks changed and proves the rest are identical in IR.
*Deliverables:* Targeted network edit mode — AI changes only the named network(s); surrounding-logic invariance check; before/after diff for review.
*Exit:* Ten modification tasks with zero unintended changes outside the target networks.

*Ruling (recorded here 2026-08-05; owner's decision, evidence in `docs/evidence/stage-S6.md`):*
- **The entry criterion stays "S6 done" deliberately** (ruling D-4, 2026-07-18) — it was considered and kept as-is, not left unrevised. So the path is: close S6's ten fresh requests, then open S7. The S7 coding/modify skills and the invariance tool (`converter diff`) were built ahead of that gate under ruling A-4; building them is not entering the stage.

## S8 — Pattern library maturation (Goal 9)

*Entry:* S6 in use.
*Deliverables:* Grow the library from real approved generations; each pattern gains tests and usage docs per the spec; generation measured by pattern coverage (share of requests satisfiable without freeform rungs).
*Exit:* Ongoing — reviewed at each stage gate.

## S9 — Test and simulation generation (Goal 10)

*Entry:* S6/S7 in use; PLCSIM story resolved (risk R-07 — confirm whether PLCSIM Advanced supports S7-1200 G2; fallbacks: TIA-integrated PLCSIM, or an S7-1500 shadow project for logic-level testing).
*Deliverables:* AI generates test sequences (stimulus + expected response per scan) for generated/modified logic; harness drives the simulator and reports pass/fail.
*Exit:* Every S6/S7 deliverable ships with at least one passing simulated test.

> ### 🔄 STATUS, 2026-08-13 — S9 IS BEING BUILT, AND NOT THE WAY THIS ENTRY DESCRIBES
>
> **The entry condition above is superseded and R-07 is no longer on the critical path.** The test
> environment under construction drives a **real S7-1200 on a bench rig over Modbus TCP** — not a
> simulator — so *"confirm whether PLCSIM Advanced supports S7-1200"* stopped being the gating
> question. **The word "simulated" in the deliverable and exit lines should be read as "executed",**
> and the simulation route survives only as a fallback nobody currently needs. R-07 is not closed;
> it is **no longer blocking**, which is a different thing and is why it stays in the register.
>
> **Design:** `docs/notes/PC-Client-Modbus-Spec-Draft-final.txt` (with `modbus-tcp-client-spec.md`,
> `modbus-plc-side-contract.md`, `test-environment-contract.md`, `assertion-enumeration.md`).
> **Live state:** `docs/notes/test-environment-build-plan.md` — that file is the standing record and
> this entry deliberately does not duplicate it.
>
> **Where it has got to:** ***PHASES 0, 1, 2 AND 3 ARE CLOSED AND VALIDATED ON THE DEVICE***, and
> phase 4.3/4.4 plus the result package are built. Six of the design's unmeasured assumptions are
> retired — **A1** (`MB_SERVER` write atomicity, at full width and both call orders), **A2**
> (round-trip rate), **A3** (`%MW` capacity), **A4** (a block driveable by its own command), **A5**
> (two slots do not interfere) and **A7** (run-state read in both CPU states). **A6** (a delegate
> throw leaving the CPU untouched) is the widest-blast-radius item still unmeasured and needs the
> owner present; **A8/G2** is deferred by ruling.
>
> 🛑 **SUSPENDED 2026-08-17 along with the rest of the plan.** This block described work in
> progress; that work is **stopped where it stands, not cancelled and not finished** — phases 0–3
> closed, phase 4.3/4.4 built, **the closed-loop conformance path never run end to end**, assumption
> **A6** still unmeasured. `docs/notes/test-environment-build-plan.md` and
> `docs/notes/live-project-readiness.md` remain accurate as of that date; read the readiness page
> before using any of the harness on a live job, since the tooling itself is not suspended.
>
> ***THE EXIT CRITERION ABOVE IS UNCHANGED AND IS NOT YET MET.*** Phase 2 demonstrated a deliberate
> defect going RED and its correction going GREEN — *and the same defect shown invisible under a
> different vector*, so the harness is not one that reddens everything. That is the gate for the
> machinery, not for the criterion: no S6/S7 deliverable ships with a passing executed test yet.

## Sequencing rationale

Read before annotate, annotate before review, review before generate, generate before modify, everything before simulate-and-verify. Each stage builds the trust and infrastructure the next depends on. Goal 7 (compile gate) and Goal 11 (safety exclusion) are invariants, not stages.
