# PlantAutoControl-bench-rerun — Architecture manifest (PARTIAL — NOT GATE-1 SIGNABLE)

Emitted by `/gen-code-structure` (rung D) as the handoff `gen-block-new` requires. **It is
deliberately incomplete, and it must not be signed.**

## Why this file is partial

The rung-D skill requires that "each manifest item embeds its D3 render verbatim, with the
relation-id tag on every term". **D3 was not produced**: the skill's own stop condition fired for all
six instances (unresolved BLOCKING questions landing on terms that would be rendered). So the skill
mandates emitting a manifest whose required content cannot exist. Rather than fabricate renders to
satisfy the format, this file carries everything that IS determined and states plainly what is
missing. That collision between the two rules is reported as a skill defect, not worked around.

- **Produced:** 2026-08-04. Scope: six instances of `PlantAutoControl-bench`.
- **Trace target:** `gen/PlantAutoControl-bench-rerun/requirements.md` (166 REQs, derived at rung C).
- **Structure source:** `gen/PlantAutoControl-bench-rerun/code-structure.md` (D0/D1/D2 complete, D3 stopped).

## Block manifest

| item | kind | status | notes |
|---|---|---|---|
| orchestration block (unnamed) | FB or FC, LAD | **PROPOSED — not named, not numbered** | The per-equipment automatic-control layer: drives each equipment FB's inputs, calls it, drives the field outputs. Naming it and allocating its number is the engineer's (hard rule 3) — Q-D2. |
| `FilterUnitSystem` | FB, existing | reuse unchanged | `ir/PlantAutoControl-bench/FilterUnitSystem.ir`, 14 networks |
| `MotorVSDSystem` | FB, existing | reuse unchanged | `ir/PlantAutoControl-bench/MotorVSDSystem.ir`, 16 networks |
| `TomraControlSystem` | FB, existing | reuse unchanged | `ir/PlantAutoControl-bench/TomraControlSystem.ir`, 20 networks |
| `FilterUnitInst2` / `FilterUnitInst3` / `FilterUnitInst1` | instance DBs, existing | reuse unchanged | `FilterUnitInst2.ir` / `FilterUnitInst3.ir` / `FilterUnitInst1.ir` |
| `MotorVSDInst1` / `MotorVSDInst3` | instance DBs, existing | reuse unchanged | `MotorVSDInst1.ir` / `MotorVSDInst3.ir` |
| `TomraControlInst1` | instance DB, existing | reuse unchanged | `TomraControlInst1.ir` |

**No new FB type, no new instance DB, no new global DB.** This generation is pure orchestration over
an existing library.

## DB landscape (all given, all `exists`, none modified by this work)

| DB | role |
|---|---|
| `PlantControl` (`Control.ir`) | plant run state, sub-system enables, shutdown-ready flags, pre-start complete |
| `HMIControlSignals` (`HMIControlSignals.ir`) | operator commands, global reset, per-conveyor rotation bypasses, global overrides |
| `DiscreteInputs` (`Input.ir`) | field inputs |
| `DiscreteOutputs` (`Output.ir`) | field outputs |
| `InterlockData` (`PLC.ir`) | third-party plant interlock |
| `ProcessTimings` (`Timings.ir`) | named plant timings; this work uses `NormalFanStartTime` only |

## OB1 call order (proposed)

Not determinable from this run alone: only 6 of the plant's 20 machines are in scope, and the start
cascade crosses machines that have no spec. What IS determined is that the orchestration block is a
single cyclic call and each equipment FB is called once per scan from within it — no order-dependent
inter-block sequencing is implied by any relation in the ledger. Fixing the order needs the other 14
machines specified.

## Pattern / tier mapping

None. `patterns/` does not exist in this repo (Q-D1). Block fit was whole-block reuse against the
library FBs directly; the D0 shapes (S1–S5) were declared from first principles and are unvalidated
against any pattern library.

## REQ → block trace

All 166 REQs of `requirements.md` land on the single proposed orchestration block, distributed as:

| disposition | REQs | where it lives |
|---|---|---|
| satisfied inside the reused FB | 96 | `FilterUnitSystem` / `MotorVSDSystem` / `TomraControlSystem` networks, cited per row in the D2 ledger |
| would render as a term in the orchestration block | 37 + 2 rebound | the orchestration block — **terms not yet written (D3 stopped)** |
| blocked by an unresolved question | 19 | the orchestration block — cannot be written |
| discharged into a sibling instance's network | 4 | `FilterUnitInst2` P1, `FilterUnitInst3` P1, `TomraControlInst1` P3, `MotorVSDInst3` P1/P2 |
| out-of-scope obligation on a machine with no spec | 8 | `AirStarInst1`, `MotorStarterInst8`, `MotorStarterInst6/7`, `ECSControlInst1`, `ShredderControlInst1` |

Row-level trace: `gen/PlantAutoControl-bench-rerun/code-structure.md` §D2.

## Cross-instance wiring

Determined (interface members, entering legitimately at rung D):

- `FilterUnitInst2/3` start permissive ← `MotorVSDInst3.IO.UPSEnable`
- `FilterUnitInst2/3` shutdown hold ← `AirStarInst1.IO.ShutdownComplete` **and** `PlantControl.FansShutdownReady` (two terms — the S3 discharge was rejected)
- `FilterUnitInst1` shutdown hold ← `ShredderControlInst1.IO.ShutdownComplete` **and** `PlantControl.FansShutdownReady`
- `MotorVSDInst1` start permissive ← `MotorStarterInst8.IO.UPSEnable` and `ECSControlInst1.IO.UPSEnable`; shutdown hold ← `AirStarInst1.IO.ShutdownComplete`
- `MotorVSDInst3` start permissive ← `TomraControlInst1.Outputs.UPSEnable` **and** `DiscreteInputs.TomraReady` **and** `NOT TomraControlInst1.Outputs.FaultActive` (rebound at D1 to the broader guard) and `MotorStarterInst6/7.IO.UPSEnable`; shutdown hold ← `ECSControlInst1.IO.ShutdownComplete`
- `TomraControlInst1` start permissive ← `MotorStarterInst6/7.IO.UPSEnable`; shutdown hold ← `MotorVSDInst3.IO.ShutdownComplete`

Undetermined: `FilterUnitInst1`'s start permissive (A Q-04) and its reverse into the Shredder
(A Q-05); `MotorVSDInst1`'s reverse into the Shredder (A Q-05).

## Tag status

`converter tagstatus --project ir/PlantAutoControl-bench` over every signal named across rungs C and D:
**0 proposed, all `exists`.** Where a requirement needs a signal that does not exist
(`FilterUnitSystem*IsoFB`, `TomraIsoFB`, `AirStarDCRunning`, `OSCRunning`, a per-machine hand selection,
the sorter's data link), **no tag was proposed and none was invented** — those are carried as
blocking questions (Q-C02, Q-C09, Q-C13, Q-C16, Q-C18, Q-C19).

## Gate 1

**NOT SIGNABLE.** See the gate block at the end of `code-structure.md`: D3 is absent, two HARD FAIL
conditions are open (`IO.SystemHealthy`, `IO.RotationSensor`), and 28 blocking questions across four
rungs are unanswered. `gen-block-new` must not be run against this file.
