---
name: gen-code-structure
description: Rung D of the structured spec pipeline — turns the per-equipment control specs into a code structure (gen/<project>/code-structure.md) — declared logic shape, library-block fit, a discharge ledger, and the per-instance boolean render (AND/OR/NOT + interface members). Use when asked to structure the code, choose blocks against a spec, define block boundaries/interfaces, or render specs into explicit interlock logic. Booleans and interface members enter HERE and nowhere earlier. Runs inside the lad-coder sub-agent. Ladder-AI project.
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
  - Write
  - Edit
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe:*)
---

# /gen-code-structure — rung D: shape, fit, discharge, render

Ladder-AI project. Fourth rung of the structured spec pipeline (A → B → C → D):
A `gen-pid-analysis` → B `gen-functional-analysis` → C `gen-equipment-spec` → **D this skill**.
Its output feeds the Build coding stage (`gen-block-new`). Read `CLAUDE.md` first.

**You run inside `lad-coder`** (hard rule 8) — you render ladder-level logic. If reached any other
way, stop and require dispatch.

**Booleans and interface members enter at this rung.** Everything above stayed at process
abstraction; here the spec becomes explicit `AND` / `OR` / `NOT` over real interface members.

## Inputs

- **`gen/<project>/equipment-specs/*` (rung C) — REQUIRED.** The per-instance control specs.
  **Stop condition:** a spec carrying an unresolved **BLOCKING** `Q-nn` that affects logic you would
  render → stop and report; never render a guess for a contested requirement.
- **`patterns/` + the library blocks (`ir/<project>/`)** — the reuse vocabulary. Read the full IR of
  any block you propose to reuse; its real interface is ground truth.
- **`docs/06-lad-conventions.md`** — read fresh (priority order, stricter generated-code bar).

## Method — shape FIRST, then fit, then discharge, then render

### D0 — Declare the logic shape BEFORE rendering anything
Name the structure the spec set implies: `permissive-chain`, `staged-sequence-with-fault-interrupts`,
`combinational-interlock`, … For each declared shape state:
- **what it discharges** — the class of spec relations it satisfies structurally;
- **the argument** — why it satisfies them;
- **the preconditions** the argument depends on.

Declaring the shape first is what turns "I see a pattern for this" from an accident into a decision.

### D1 — Block fit (reuse-first)
For each equipment class, find the library block that covers its requirement set. **A block carrying
MORE than the spec requires is a valid fit** — extra capability in a proven block is accepted, not
flagged (C-606 whole-reuse exception). Produce a coverage table: each `C-nn` → satisfied inside the
FB / to be wired by orchestration. Group the per-instance specs into FB types + instance DBs here
(rung A expanded per instance precisely so this grouping could be *derived*, not assumed).
**Settings:** honour the spec's owner field — an HMI-owned setting is left at its instance default,
never cyclically copied onto (the C-308 scan-copy trap).

**Binding audit (mandatory).** For every rung-C binding, check it against the FB interface you have
just read. If the FB exposes a signal that satisfies the relation **more broadly or more strongly**
than C's binding (an aggregated `FaultActive` vs a raw comms fault; a confirmed-running member vs a
ready member), you may **neither** silently keep C's binding **nor** silently re-bind: record a
`rebind` ledger entry naming both candidates, state what the FB does with each member, and prefer the
**stronger/broader guard**. Rung C had to choose a signal before this interface was ground truth;
this is the sanctioned back-edge.

**Undriven-input audit.** Enumerate the chosen FB's input/interface members and mark each **driven**
(cite the D3 term) or **defaulted** (state why). An undriven input whose name matches an unclaimed IO
signal is a **HARD FAIL**. *A real VSD FB declares `IO.RotationSensor : Bool`; a generation left it
undriven while its bypass tag sat unreferenced (`docs/evidence/PlantAutoControl-bench-grading.md`,
REQ-012).*

### D2 — Discharge ledger (the safety-critical section)
Every spec relation (`C-nn`, `P-nn`) must be accounted for exactly once:
- **rendered** — it becomes an explicit term in D3; or
- **satisfied inside the reused block** — cite the FB and its mechanism; or
- **discharged by a declared shape** — cite the D0 shape, its argument and preconditions.

**A discharge must be explicit, argued, and preconditioned — and its precondition must be VERIFIABLE
FROM ARTIFACTS IN SCOPE.** Classify every precondition as one of: **`verified-in-block`** (cite the
network), **`verified-cross-block`** (cite the block, file and line in `ir/<project>/`), or
**`unverifiable`**. **An `unverifiable` precondition is not a valid discharge — render the relation
as a term instead.** *"The plant flag probably already encodes the neighbour's shutdown-complete" is
exactly such an argument: the flag is computed in another block, outside this generation's boundary,
and the real regression it would have licensed is recorded in
`docs/evidence/PlantAutoControl-bench-grading.md` §4-A. Plausibility is not verification.*

An undeclared discharge — quietly dropping a term because "something else probably covers it" — is
the documented cause of that regression (`docs/evidence/PlantAutoControl-bench-autopsy.md`). Silence is
the defect; a verified argument is the fix. **A relation with neither a term nor a ledger entry is a
HARD FAIL.**

**Emit the ledger as a machine-checkable table** — one row per relation:
`relation-id | disposition (rendered / in-FB / discharged / rebind) | evidence | precondition class`.
The relation-id column must set-difference to **empty** against the union of all `C-nn`/`P-nn` ids in
`gen/<project>/equipment-specs/`. State both counts explicitly in the artifact.

### D3 — Render, per instance
Explicit boolean over the real interface members. **The ladder carries only what is needed to satisfy
the spec**: every term traces to an undischarged relation, and a term tracing to nothing is a finding
(unjustified capability, C-606). Tag each term with the relation id it satisfies.

```
NETWORK "Conveyor-07 Automatic Control"        instance: iDB_MotorDOL_Conv07
  .RunningFB       := DI_23                                                    [C2]
  .InhibitMotor    := DI_24                                                    [C3]
  .AutoStartSignal := PlantRunning AND Conveyor08.UPSEnable AND PreStartComplete  [P1,P2]
  .Shutdown        := PlantShutdown AND NOT Conveyor08.ShutdownComplete
                                    AND NOT .HandIntervention                  [P3,C6]
  DQ_11            := .Run                                                     [C1]
  CALL MotorDOL(iDB_MotorDOL_Conv07)
```

## Output — `gen/<project>/code-structure.md`

Provenance header, then **D0 shape declarations**, **D1 block-fit + grouping**, **D2 discharge
ledger**, **D3 per-instance render**, then open questions. Ends with a gate-1 sign-off block (the
engineer signs before the Build coding stage starts — `docs/11-review-workflow.md`).

**Also emit/update `gen/<project>/architecture.md`** — the block manifest in `gen-architecture`'s own
output shape (manifest, interfaces, DB landscape, OB1 order, pattern/tier mapping, REQ→block trace,
cross-instance wiring, tag status). **This is a handoff requirement, not a nicety:** `gen-block-new`
STOPs without a gate-1-signed `architecture.md`, so a project structured through these rungs cannot be
coded at all unless this view exists. Rung C's derived `requirements.md` is the trace target its
REQ→block table cites.

## Calibration

- **Ambiguity is never resolved silently.** If a spec relation admits two renderings, stop and raise
  it; where forced to choose on **any protective term — interlock, permissive, inhibit or fault
  gate** — prefer the **stronger/fail-safe** guard and record the choice (FI-34). *(In this repo
  "safety" means F-content that must be refused outright, hard rule 2 — this rule is about ordinary
  process protection, which is exactly where the documented misses happened.)*
- **Only as complex as the spec needs** — the skeptic's single reading must succeed (C-601/C-602).
  A 19-term enumerated chain that a declared shape could discharge is a defect of this stage, not
  fidelity.
- **Never invent tags, addresses or DB numbers** (hard rule 3). **Safety content = stop** (hard rule 2).
- **This rung structures; it does not code.** The IR itself is `gen-block-new`'s job.
- **Telemetry:** append one line to `gen/<project>/telemetry.log` per `docs/notes/gen-telemetry.md`.
