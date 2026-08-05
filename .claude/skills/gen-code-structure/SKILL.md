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
  # undriven-scan (per-instance interface drive states) and candidate-scan are covered by the
  # wildcard above; named here so the wiring is greppable.
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe undriven-scan:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe undriven-scan:*)
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

**Undriven-input audit — COMPUTED, per instance.** Run
```
converter undriven-scan --project ir/<project>/ --fb <FBType> [--caller <generated-block.ir>]
```
It classifies every interface member of **every instance** as `driven` / `disarmed` (writers exist but
all provably-false-gated) / `undriven (default X)` / `undriven` / `dead-interface` (no writer **and**
the FB never reads it either). **Per-instance is the point:** the project-wide reference graph pools
across all instances of an FB, so one driven instance masks an undriven sibling — and a dropped input on
*one* instance of a shared block is exactly that shape (`docs/evidence/PlantAutoControl-bench-grading.md`,
REQ-012, where a real VSD FB declares `IO.RotationSensor` that nothing wires and nothing reads).

- **`undriven` / `disarmed` are HARD FACTS** (the tool exits 1): the FB consumes a value nothing sets.
  Resolve or record each.
- **`dead-interface` does NOT auto-fail** — a reusable block legitimately exposes optional inputs an
  instance doesn't use. It is a fact to **cross against the spec**: if a `C-nn`/`P-nn` relation required
  that capability, a dead interface member is how you find it was never wired.
- `--hints` (name-token match against unreferenced project signals) is opt-in, heuristic, and never part
  of the finding.

Before the tool existed this audit was prose, and the run that wrote it as prose still missed the
member — **the tool computes the fact; you still own whether the spec required it.**

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

**Emit the ledger as a machine-checkable table.** `converter relation-reconcile` parses this, so the
shape is a CONTRACT, not an illustration — write it exactly:

```
## Ledger — `FilterUnitInst2` (Dust Filter Unit 1)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | render-BLOCKED | Q-C04 — no hand/auto source | — |
| C3 | in-FB | `FilterUnitSystem.ir` N1 (`NOT IO.FaultActive`) | verified-in-block |
```

- One `## Ledger — \`<Instance>\`` heading per instance — the instance is read from the **backticks**.
- The header row's first cell is literally `relation`; the last is `precondition class`.
- Relation ids are **`C1`/`P1`** — unhyphenated, unpadded. (`C-nn` is prose for the *set*, never an id.)
- An empty precondition cell is an em-dash `—`.
- **Evidence must contain at least one backticked identifier that resolves in the export.** A cell
  citing only a network number (`N1`, `N10`) carries no checkable claim, and the citation check will
  flag the row. Cite the member, not just where you looked.

**Dispositions** — the complete vocabulary; do not invent a sixth:

| Disposition | Meaning |
|---|---|
| `rendered` | it becomes an explicit term in D3 |
| `in-FB` | satisfied inside the reused block (cite the FB network) |
| `discharged` | covered by a declared D0 shape (cite the shape) |
| `rebind` | D1's binding audit moved it to a stronger signal |
| `render-BLOCKED` | would be a term, but a **blocking `Q-nn` contests it** — cite the Q |
| `render-stopped` | would be a term; **no Q contests it**, but the whole instance's render is stopped |
| `out-of-scope-obligation` | the counterpart lives outside this run's scope — cite the owed obligation |

`render-BLOCKED` requires a contesting `Q-nn`; **`render-stopped` is for the uncontested majority of a
stopped instance**, which has no Q to cite. *Two consecutive runs invented their own marker for exactly
this state because the vocabulary lacked it — if you find yourself inventing a disposition, the gap is
in this contract and belongs in a friction report.* The last three **count toward the set-difference**,
are **never** discharges, and **never** carry a precondition class.
The relation-id column must set-difference to **empty** against the union of all `C-nn`/`P-nn` ids in
`gen/<project>/equipment-specs/`. State both counts explicitly in the artifact.

### D3 — Render, per instance
Explicit boolean over the real interface members. **The ladder carries only what is needed to satisfy
the spec**: every term traces to an undischarged relation, and a term tracing to nothing is a finding
(unjustified capability, C-606). Tag each term with the relation id it satisfies.

The render is also machine-read (`relation-reconcile`'s fourth leg), so its shape is a contract too:
each network states `instance: <iDB>` on its header line, and **every term carries its relation-id tag**
`[C2]` / `[P1,P2]`.

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

**When D3 is stopped**, say so with a heading containing the word `STOPPED` (e.g.
`## **STOPPED — all six instances. No render produced.**`). The reconciler then reports the render leg
as **`ABSENT`** rather than as a leg that trivially agrees — which is the difference between "this rung
legitimately stopped" and "this rung produced nothing and nobody noticed."

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

Each manifest item in the emitted `architecture.md` **embeds its D3 render verbatim**, with the
relation-id tag on every term, and cites its D2 ledger rows. The manifest is the coder's only required
input; a render that lives only in `code-structure.md` does not reach the coder.

**Where D3 is stopped, emit the manifest in PARTIAL form** — this rung's own stop condition can
legitimately prevent the render existing, and the requirement above must never become pressure to
invent one. A partial manifest carries every determined item, states per affected item *"renders
absent — D3 stopped, see `<Q-ids>`"*, and ends with a **NOT SIGNABLE** gate block. **Never fabricate a
render to satisfy this section, and never emit a manifest that looks complete when it is not.**

## Self-check before you finish — run the checkers on your OWN artifacts

```
converter relation-reconcile --specs gen/<project>/equipment-specs --ledger gen/<project>/code-structure.md \
                             --register gen/<project>/requirements.md --project ir/<project>/
```

**A leg it cannot parse, a non-empty set-difference, or a citation finding is a defect in YOUR
artifacts, not in the tool** — fix and re-run before finishing. *A real run's first ledger was rejected
outright (0 rows parsed) and then showed 60 citation findings whose evidence cells named only network
numbers; both were genuine and both were fixed before the artifact was presented.* Report the final
result in your exit summary — a reconciliation you didn't run is not evidence.

## Calibration

- **Ambiguity is never resolved silently.** If a spec relation admits two renderings, stop and raise
  it; where forced to choose on **any protective term — interlock, permissive, inhibit or fault
  gate** — prefer the **stronger/fail-safe** guard and record the choice (FI-38). *(In this repo
  "safety" means F-content that must be refused outright, hard rule 2 — this rule is about ordinary
  process protection, which is exactly where the documented misses happened.)*
- **Only as complex as the spec needs** — the skeptic's single reading must succeed (C-601/C-602).
  A 19-term enumerated chain that a declared shape could discharge is a defect of this stage, not
  fidelity.
- **Never invent tags, addresses or DB numbers** (hard rule 3). **Safety content = stop** (hard rule 2).
- **This rung structures; it does not code.** The IR itself is `gen-block-new`'s job.
- **Telemetry:** append one line to `gen/<project>/telemetry.log` per `docs/notes/gen-telemetry.md`.
