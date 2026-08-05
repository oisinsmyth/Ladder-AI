# PlantAutoControl-bench — Architecture Manifest (Design stage, gate-1 input)

Design-stage output for regenerating **`PlantAutoControl`** — the per-equipment automatic-control
layer of a shredding / material-recovery plant. Produced by `gen-architecture` (docs/15 stage 5).
This artifact is a **proposal**: nothing downstream (the Build coding stage) starts until the
engineer signs the gate-1 block at the end.

## Provenance

- **Date:** 2026-07-20. **Skill:** `gen-architecture`.
- **Inputs consumed (git hash):**
  - `gen/PlantAutoControl-bench/requirements.md` — `5cd2c71` (REQUIRED; the WHAT this design derives from).
  - `ir/PlantAutoControl-bench/` export — `ae0414a` (the fixed boundary: 6 global DBs, 8 equipment FB
    types, 20 instance DBs).
  - `docs/06-lad-conventions.md` — `1e90300` (read fresh this run).
  - `patterns/chained-permissive-enable/pattern.md` — `31f68b5`; `patterns/motor-dol/pattern.md`,
    `patterns/input-mapping/pattern.md` (read this run); `docs/07-pattern-library-spec.md`.
- **Designed WITHOUT `process-topology.md`** — the material-flow *direction* used in sections 4/8 is
  reverse-derived from the register's dependency table (Q-02), marked **provisional**; the enable
  logic is correct regardless of the direction rationale.
- **Designed WITHOUT `io-map.md`** — physical addressing stays entirely out of this design. This
  block reads/writes only **buffer-DB members** (`DiscreteInputs.*` / `DiscreteOutputs.*`); the
  physical↔buffer Map FCs (`FC Inputs`/`FC Outputs`) are separate blocks, out of this regeneration
  scope.
- **Designed WITHOUT `rfi.md`** — the register's 8 open questions (Q-01…Q-08) surface here as
  carried open questions, not resolved answers.
- **Independence declaration — INFORMED, not fully independent.** The task directed me to read
  `patterns/chained-permissive-enable/pattern.md`, a sanctioned composition input. That pattern was
  **extracted from the regeneration target itself** and embeds four real example networks (2, 7, 8,
  12) of `PlantAutoControl`/`PlantAutoControl` with their coil formulas. My design's per-equipment
  network *shape* is therefore informed by the target's own documented rung shape — this is the
  intended pipeline workflow (compose from proven patterns), but a gate-1 reviewer should weigh the
  block-shape decisions as pattern-informed, not blindly re-derived. I did **not** read the sealed
  answer key (`docs/evidence/PlantAutoControl-answerkey/`), the `patterns/*/examples/*.ir` files, or any
  as-built implementation beyond the pattern docs and the given library-block boundary.
- **Safety:** hard rule 2 not triggered. No F-block / safety-program / E-stop content is read,
  written, or referenced. The E-stop feedback members present in the given DBs
  (`PlantControl.ShredderEStopFB`, `InterlockData.OldPlantEStopZone1..3`) are deliberately untouched
  by this design (REQ-022).
- **Tooling note:** the Release `converter` binary is absent in this worktree, so the five optional
  bash aids (`tagstatus`/`reuse-scan`/`target-scan`/`digest`/`review`) were skipped per the skill's
  fallback; tag status (section 9) was established by direct grep/read of the export instead.

---

## 1. Block manifest

The regeneration target is a **single FC** — `PlantAutoControl` — that reads the plant run state and
field-feedback buffers, computes each machine's automatic start/stop/interlock permissives, drives
the physical run outputs (buffer side), and calls each machine's own equipment FB. This matches the
register's stated scope (one block, 20 per-equipment networks) and the `chained-permissive-enable`
pattern's own rationale: it is the **control area-Main FC**, kept as one block so it reads top-to-
bottom like a table of contents (C-109/C-110), one network per machine.

| Item | Kind | Created / touched | Purpose | Carving tier (§6) |
|---|---|---|---|---|
| `PlantAutoControl` | FC | **created** (regeneration target) | 20 networks, one per equipment: map feedback in, compute auto permissives, drive run output, `CALL` the equipment FB | (b) + small (d) |
| `MotorStarter` | FB type | given, reused | DOL motor equipment FB (`motor-dol` pattern) | (a) |
| `MotorVSDSystem` | FB type | given, reused | VSD-driven motor equipment FB | (a) |
| `MotorFwdRevSystem` | FB type | given, reused | forward/reverse motor equipment FB (feed conveyor) | (a) |
| `AirStarSystem` | FB type | given, reused | air-separator VSD equipment FB | (a) |
| `FilterUnitSystem` | FB type | given, reused | FilterUnitSystem equipment FB | (a) |
| `EquipmentControlSystem` | FB type | given, reused | ECS equipment FB | (a) |
| `TomraControlSystem` | FB type | given, reused | optical-sorter equipment FB | (a) |
| `ShredderControlSystem` | FB type | given, reused | shredder equipment FB | (a) |
| 20 instance DBs | instance DB | given, wired | `MotorStarterInst1..9`, `MotorVSDInst1..3`, `FilterUnitInst1..3`, `AirStarInst1`, `ECSControlInst1`, `TomraControlInst1`, `MotorFwdRevInst1`, `ShredderControlInst1` — the machines `PlantAutoControl` calls | (a) |
| `PlantControl`, `HMIControlSignals`, `DiscreteInputs`, `DiscreteOutputs`, `InterlockData`, `ProcessTimings` | global DB | given, read/written | run-state / HMI signals / input buffer / output buffer / third-party interlock / timings | (a) |
| `OB1` | OB | given, wired-into | must `CALL PlantAutoControl` between input-map and output-map (see §5) — **not** in this regeneration's given export; wiring flagged (Q-10 NEW) | — |

**Name note (C-003):** the frozen/sealed target block is named `PlantAutoControl` (sanitized
namespace) / `PlantAutoControl` (live), without doc-06's `FC_` prefix. I keep the frozen name (C-004 —
Openness matches by name; this is a regeneration, not a new block) and route the missing-prefix
observation to convention review (Q-10 NEW), rather than silently rename it.

---

## 2. Interface definitions (the GIVEN equipment interface)

The register fixes the equipment interface as the **Phase-2 boundary — interface design is not under
test here.** This design *wires to* that interface; it does not define it. The shared handshake
vocabulary below is grounded from the one FB whose interface resolves in full in the export
(`MotorStarter`, `IO : "MotorIOSet"`); the other seven FB types' members are given-boundary and are
verified member-by-member only at the compile gate (member-level existence is TIA's check, §9).

**C-115 handshake vocabulary** — the shared `IO.*` members the enable chain wires identically on
every equipment FB:

| Shared member | Dir | Role | Conformance |
|---|---|---|---|
| `AutoStartSignal` | in | this instance's computed auto run-permission | all 8 FB types (given) |
| `RunningFB` | in | raw running feedback | all 8 |
| `SystemHealthy` | in | health/permissive gate (this block asserts TRUE — REQ-022) | all 8 |
| `InhibitMotor` | in | hard inhibit (isolator) | all 8 |
| `PreStartDone` | in | plant pre-start-complete gate | all 8 |
| `FaultReset` | in | reset command (plant `SystemReset`) | all 8 |
| `HandIntervention` | in/read-back | hand-mode flag; **set/reset by the FB itself**, read back by callers computing a *neighbour's* `Shutdown` | all 8 |
| `Shutdown` | in | controlled-shutdown request | all 8 |
| `Run` | out | commanded run → physical output | all 8 |
| `UPSEnable` | out | "up to speed long enough" → the next machine's `AutoStartSignal` (the enable-chain link) | all 8 |
| `ShutdownComplete` | out | shutdown-done → releases the neighbour holding on it | all 8 |
| `Reverse` / direction members | in | forward/reverse selection | `MotorFwdRevSystem` only (REQ-016) |

**Settings members (C-307 / C-308).** Per-instance commissioning timings (`FTTime`, `EnableUPSTime`,
`ShutdownTime`, and the VSD/fwd-rev analogues) live **inside each instance's own UDT** — HMI-written,
logic-read-only, and **`PlantAutoControl` must never scan-copy a value onto them** (the C-308 cyclic-
MOVE trap). This design writes only the *signal* members above; it writes **no** settings member.
The one plant-wide timing this block consumes — `ProcessTimings.NormalFanStartTime` (10.0 s, REQ-020)
— is read-only into the three filter units' fan-enable-time input, never written.

**Alarms (C-503).** Each equipment FB owns its own per-instance alarm surface inside its UDT
(`IO.Alarm` word, `IO.Telemetry`); `PlantAutoControl` neither packs category alarm words nor
duplicates per-instance alarms. Alarm *design* (texts, severities, suppression) is `gen-alarm-design`
scope, not this artifact.

---

## 3. DB landscape

Every DB this block touches already exists in the given boundary; **this design creates no DB.**

| DB | Role | This block |
|---|---|---|
| `PlantControl` (#39) | plant run state + sub-system enables: `Status`, `PreStartComplete`, `GeneralEnable`/`DrumsEnable`/`MagEnable`, `DrumShutdownReady`/`FansShutdownReady`, `AutoPreStart`, `PositiveEdgeArray[]` | reads all; **writes `AutoPreStart`** (S/R, REQ-015) |
| `HMIControlSignals` | operator command surface: `SystemReset`, per-conveyor `Bypass*RotSen` | reads only |
| `DiscreteInputs` | field-input buffer (C-304): `*Running`/`*Op`, `*RotSen`, `*IsoFB`, filter/sorter/ECS/cyclone feedbacks | reads only |
| `DiscreteOutputs` | field-output buffer (C-304): `*Start`/`*RunFwd`/`*RunRev`/`TomraRun`, `RunPreStart`, `*Reset` | **writes** (run/reverse/reset/pre-start outputs) |
| `InterlockData` | third-party interlock + PLC system data: `JOB9001Interlock`, `JOB9001ShutdownComplete`, `LinkOut3rdParty`; also `Simulation`, `NotFirstScan` | reads `JOB9001*`/`LinkOut3rdParty` |
| `ProcessTimings` | plant-wide timings; only `NormalFanStartTime` (10.0) used here | reads `NormalFanStartTime` |

**Doc-06 skeleton deviations (routed, not adjudicated — existing site structure this block does not
change):** the boundary has **no separate `DB_PLC`** (the C-305 `Simulation` bit + a first-scan bit
live inside `InterlockData` instead), and **no separate `DB_Alarms`/`DB_Timers`/`DB_Settings`/
`DB_Controls`** — the site consolidates command onto `HMIControlSignals`+`PlantControl`, plant-wide
settings onto `ProcessTimings`, per-instance settings/alarms into the equipment UDTs (which *does*
match C-307/C-503). These are pre-existing boundary facts; routed to `/review-conventions` if the
engineer wants them assessed, not changed by this regeneration.

**OB100 / startup-reset machinery (C-403/C-305/C-124/C-128) — OUT of this regeneration's scope, and
flagged, not silently omitted.** `PlantAutoControl` writes at least one S/R-driven state bit
(`PlantControl.AutoPreStart`, REQ-015), and the reused equipment FBs hold their own internal S/R
state (`MotorStarter.IO.StopMotor` SCOIL/RCOIL, etc.). C-403 requires every such bit be cleared once
at startup by an OB100 block. That OB100 block is a **separate block against a boundary that contains
no `DB_PLC`** — I will not invent it (hard rule 3). Recorded as **Q-11 (NEW)**: confirm a startup-
reset block exists elsewhere in the project for these bits, or record an owner waiver. No waiver is
on record in the inputs, so this is a named gap for the engineer, per the skill.

---

## 4. Command-flow / enable graph

Per the register's C-113 memory test this plant is **combinational chained-permissive** (C-114), not
a stepped sequence — so this is a command-flow / enable graph, not a step chart. The plant run-state
word `PlantControl.Status` is the single conditional head; each machine's `AutoStartSignal` is gated
by an **upstream neighbour's `UPSEnable`** (enable edge, one direction) plus its own readiness, and
the staged shutdown propagates via **neighbour `ShutdownComplete`** (interlock edge).

**Enable edges (start permissives, REQ-004) — "X enabled → Y may start"** (direction *provisional*,
Q-02):

```
Status>=1 (+PreStartComplete) ──head──> the chain
AirStarVSD.UPSEnable      → InclineFeed(MotorVSDInst2)
InclineFeed.UPSEnable     → LinkConv(MotorStarterInst4)      [+ JOB9001 interlock]
SorterConvVSD.UPSEnable   → FilterUnitInst2/2(FilterUnitInst2/3), ECS(ECSControlInst1)
MotorStarterInst81+ECS.UPSEnable → DischargeConvVSD(MotorVSDInst1)
MetalCollConv.UPSEnable   → MotorStarterInst81(MotorStarterInst8)
OpticalSorter ready + Ejected+Residual enabled → SorterConvVSD(MotorVSDInst3)
Ejected+Residual enabled  → OpticalSorter(TomraControlInst1)
Feed enabled              → DiscSpreader1/2(MotorStarterInst1/2)
DiscSpreaders enabled     → Residual(MotorStarterInst7), Feed-fwd(MotorFwdRevInst1)
DischargeConv enabled     → Shredder(ShredderControlInst1)
```

**Plant-flag enable heads (REQ-013, sub-system gates — chain-head machines, no upstream `UPSEnable`):**
`MagEnable`→MetalCollConv; `DrumsEnable`→DrumSeparator; `GeneralEnable`→Ejected/Residual/Discharge/
Feed-rev; cyclone filter runs with the plant.

**Interlock edges (NOT enables — exempt from C-114's one-direction rule):** every neighbour
`ShutdownComplete` reference (staged shutdown, REQ-003) and every sub-system `*ShutdownReady`
(`DrumShutdownReady`/`FansShutdownReady`) is a process interlock and may run against flow. These are
labelled as interlocks, not counted in the acyclicity check.

**Acyclicity argument:** the enable graph uses each machine's `UPSEnable`/plant-flag as the *only*
start-permission source and each such edge points one way along the register's dependency table; no
machine's `AutoStartSignal` consumes a downstream machine's `UPSEnable`. `ShutdownComplete` cross-
references (e.g. Overband Magnet's readiness reads `MotorVSDInst1.ShutdownComplete`) are interlocks,
per C-114's explicit exemption, so they do not form enable cycles. The graph is acyclic **provisional
on Q-02** (material-flow direction unconfirmed — logic holds regardless of the rationale).

**Bidirectional equipment (C-116/C-117):** the Feed Conveyor (`MotorFwdRevInst1`, REQ-016) has **one
enable path per direction** — forward gated by shredder-ready, reverse gated by `GeneralEnable` — and
the register states the direction changes only while the conveyor is already running automatically
and not in hand. C-117's stopped-before-reverse guarantee must hold: **Q-12 (NEW)** — confirm whether
`MotorFwdRevSystem`'s own internal reversal interlock enforces it (C-117's clarified FB-internal
allowance) or `PlantAutoControl` must add the not-running term.

---

## 5. OB1 call order

Per C-110, input mapping is the first OB1 call and output mapping the last; `PlantAutoControl` (the
control area-Main) runs in between so it sees fresh inputs and the field receives the current scan's
decisions.

```
OB1:
  1. CALL FC Inputs            -- physical→DiscreteInputs buffer (separate block, out of scope)
  2. CALL PlantAutoControl     -- THIS block (control area-Main)
  …  (other area Mains, if any — out of scope)
  n. CALL FC Outputs           -- DiscreteOutputs buffer→physical (separate block, out of scope)
```

**Provisional:** OB1 and the Map FCs are not in this regeneration's given export; the call-order
requirement is stated so the Build stage and the engineer wire `PlantAutoControl` correctly (Q-10
NEW). No OB100 call list is designed here — see §3 (out of scope, flagged Q-11).

---

## 6. Pattern mapping

`PlantAutoControl` is one FC composed of 20 per-equipment networks. Carving pass (Method step 3):

- **Tier (a) — whole library blocks:** all 8 equipment FB types and 20 instance DBs are reused
  as-is via `CALL`. `MotorStarter` is the admitted `motor-dol` pattern; the other 7 are given-
  boundary library FBs. Extra capability these FBs carry beyond a REQ (e.g. `MotorStarter`'s hours-
  totaliser, telemetry) is **ignored, not flagged** — C-606's whole-reuse exception. *(Two known
  internal deviations exist inside `MotorStarter` — `TONR` vs C-406, a 3-bit alarm network vs
  C-501/C-301 — documented in the `motor-dol` pattern; reused as-is, routed to review, not changed.)*
- **Tier (b) — pattern-composed:** all 20 equipment networks are instances of the **admitted**
  `chained-permissive-enable` (kind-2) rung shape — `RunningFB` / `SystemHealthy` / `InhibitMotor` /
  `PreStartDone` / `AutoStartSignal` / `Shutdown` / `FaultReset` coils + optional output coil + the
  equipment `CALL`. Documented variations cover: richer `RunningFB` (rotation sensor + bypass, REQ-011/
  012), plant-flag enable source (REQ-013), and the pre-start latch (REQ-015). **Admission caveat:**
  `chained-permissive-enable` is admitted with criterion 3 (a genuinely-*blind* drafted instance)
  still owed — the engineer signs off knowing this Build stage is a candidate to close that gap
  (Q-13 NEW).
- **Tier (d) — freeform:** the Feed Conveyor's **forward/reverse direction-selection logic**
  (network 17, REQ-016) is not covered by any admitted pattern (the `chained-permissive-enable` pattern
  itself notes its VSD/reverse variants are undocumented). Structured by **C-116** (one chain per
  direction) and **C-117** (stopped-before-reverse). The optical-sorter comms-word wiring
  (`Tag_45…Tag_54`, REQ-017) is **provisional** pending Q-06 (in-scope? content?), not counted as
  committed freeform.

**Freeform % (planned-networks basis):** 1 of 20 networks carries a freeform portion (network 17's
direction selection) ≈ **~5%**. **Does NOT trip the >20% flag.** No CLAUDE.md workflow-step-3
freeform go-ahead is required, though gate-1 sign-off still covers it.

---

## 7. REQ → block traceability

Every REQ is implemented by one or more networks of the single block `PlantAutoControl` (see §1),
except the out-of-scope one. "Net" = the register's equipment-inventory row number = planned network.

| REQ | Class | Implemented by | Notes |
|---|---|---|---|
| REQ-001 | control | all 20 networks | the coordinated train |
| REQ-002 | control | all 20 (`AutoStartSignal` gated by `Status>=1`) | run-state commands start |
| REQ-003 | control | all 20 (`Shutdown` held until neighbour `ShutdownComplete`) | staged shutdown (§4 interlock edges) |
| REQ-004 | control | all 20 (`AutoStartSignal` gated by upstream `UPSEnable`) | downstream-ready start (Q-02) |
| REQ-005 | control | all 20 (`PreStartDone := PlantControl.PreStartComplete`) | pre-start gate |
| REQ-006 | mode | all 20 (`Shutdown` gated by `NOT HandIntervention`) | hand suppresses auto shutdown; Q-07 quirk on nets 10/11 |
| REQ-007 | HMI | all 20 (`FaultReset := HMIControlSignals.SystemReset`) | single global reset |
| REQ-008 | control | nets 1–4,6–17,19,20 (`DiscreteOutputs.*Start := Inst.IO.Run`) | run→physical output |
| REQ-009 | control | nets with `RunningFB := DiscreteInputs.*Running/*Op` | running confirmation |
| REQ-010 | control | nets with `InhibitMotor := DiscreteInputs.*IsoFB` | isolator inhibit |
| REQ-011 | HMI | nets 2,7,13,14,17,20 (RunningFB = running AND (rotsen OR bypass)) | per-conveyor rotation bypass |
| REQ-012 | control | net 6 (AirStar DConv: RunningFB from `AirStarDCRotSen` unless bypassed) | discharge-VSD rotation-as-running |
| REQ-013 | mode | nets 7 (`MagEnable`), 12 (`DrumsEnable`), 13/17/20 (`GeneralEnable`) | sub-system enable gates |
| REQ-014 | control | nets 1,2 (`JOB9001Interlock`/`JOB9001ShutdownComplete`) | third-party interlock |
| REQ-015 | control | net 2 (S/R on `PlantControl.AutoPreStart`, edge-detected) | auto pre-start one-shot (pattern's optional latch) |
| REQ-016 | control | net 17 (`SFCRunFwd`/`SFCRunRev` direction select) | **freeform** (§6); C-116/C-117 |
| REQ-017 | control | nets 10,11 (`TomraReady/Running/ComFlt`, `TomraRun`, comms words) | sorter integration; comms words provisional (Q-06) |
| REQ-018 | control | net 9 (`ECS*` in, `ECSAutoStart`/`ECSReset` out) | ECS integration |
| REQ-019 | control | nets 3,4,19 (filter feedbacks + `*Reset`; cyclone fault = NOT `CycloneDustSysOk`) | FilterUnitSystem mapping + reset |
| REQ-020 | timing | nets 3,4,19 (`ProcessTimings.NormalFanStartTime`→FB fan-enable-time input) | fan start-up 10 s |
| REQ-021 | control | net 5 (`AutoStartSignal` = (DConvVSD+both DustFilters) OR (DConvVSD+`LinkOut3rdParty`)) | air-sep alt permissive |
| REQ-022 | out-of-scope | **no PLC logic, by design** — `SystemHealthy := TRUE` asserted per machine | health/safety hardwired (hard rule 2) |

**No unmapped REQ.** REQ-022 is out-of-scope by design; every other REQ maps to concrete networks.

---

## 8. Cross-instance wiring plan (C-127)

Every cross-instance fact lives in the orchestrating FC `PlantAutoControl`; **no equipment FB
references a sibling by name inside its own logic** (C-127). The wires below are all written by
`PlantAutoControl`. This is the design's highest-value check — a mis-wired enable/interlock is a
gate-1-price defect here, not an end-of-line one.

| # (net) | Machine (instance) | Enable src → its `AutoStartSignal` | Shutdown interlock (`Shutdown` readiness) | Feedback in | Output out |
|---|---|---|---|---|---|
| 1 | Incline Feed (`MotorVSDInst2`) | `AirStarInst1.UPSEnable` | Link Conv shut down; `JOB9001Interlock` | `IFC*` running/iso | VSD run |
| 2 | Link Conv (`MotorStarterInst4`) | `MotorVSDInst2.UPSEnable`; `JOB9001Interlock` | `JOB9001ShutdownComplete` | running/rotsen/iso | `HFLCStart`; **S/R `AutoPreStart`** |
| 3 | Dust Filter 1 (`FilterUnitInst2`) | `MotorVSDInst3.UPSEnable` | `FansShutdownReady`/AirStar shut down | filter Ready/Op/Flt | `FilterUnit1Start/Reset`; fan time |
| 4 | Dust Filter 2 (`FilterUnitInst3`) | `MotorVSDInst3.UPSEnable` | `FansShutdownReady`/AirStar shut down | filter Ready/Op/Flt | `FilterUnit2Start/Reset`; fan time |
| 5 | Air-Sep VSD (`AirStarInst1`) | (`MotorVSDInst1`+FilterUnitInst2/2 enabled) OR (`MotorVSDInst1`+`LinkOut3rdParty`) | Incline Feed shut down | running/iso | VSD run (no discrete `*Start`) |
| 6 | Discharge Conv VSD (`MotorVSDInst1`) | `MotorStarterInst8`(mag)+`ECSControlInst1` enabled | AirStar shut down | `AirStarDCRotSen`(+bypass) | VSD run |
| 7 | Metal Coll Conv (`MotorStarterInst3`) | `PlantControl.MagEnable` | Overband Mag shut down | running/rotsen/iso | `FMCCStart` |
| 8 | Overband Mag (`MotorStarterInst8`) | `MotorStarterInst3.UPSEnable` | `MotorVSDInst1.ShutdownComplete` | `OverbandMagRunning`/iso | `OverbandMagStart` |
| 9 | ECS (`ECSControlInst1`) | `MotorVSDInst3.UPSEnable` | Discharge Conv VSD shut down | `ECS*` mode/fault/status | `ECSAutoStart`/`ECSReset` |
| 10 | Sorter Conv VSD (`MotorVSDInst3`) | Optical Sorter ready & not faulted + `MotorStarterInst6`+`MotorStarterInst7` enabled | ECS shut down | running/iso | VSD run |
| 11 | Optical Sorter (`TomraControlInst1`) | `MotorStarterInst6`+`MotorStarterInst7` enabled | Sorter Conv VSD shut down | `TomraReady/Running/ComFlt` + comms words (Q-06) | `TomraRun` |
| 12 | Drum Separator (`MotorStarterInst5`) | `PlantControl.DrumsEnable` | `DrumShutdownReady` | running/iso | `OSDrumSepStart` |
| 13 | Ejected Conv (`MotorStarterInst6`) | `PlantControl.GeneralEnable` | Optical Sorter shut down | running/iso | `OSECStart` |
| 14 | Residual Conv (`MotorStarterInst7`) | `MotorStarterInst1`+`MotorStarterInst2` enabled | Optical Sorter shut down | running/rotsen/iso | `OSRCStart` |
| 15 | Disc Spreader 1 (`MotorStarterInst1`) | `MotorFwdRevInst1.UPSEnable` (Feed) | Residual Conv shut down | running/iso | `DiskSpreader1Start` |
| 16 | Disc Spreader 2 (`MotorStarterInst2`) | `MotorFwdRevInst1.UPSEnable` (Feed) | Residual Conv shut down | running/iso | `DiskSpreader2Start` |
| 17 | Feed Conv (`MotorFwdRevInst1`) | Shredder-ready (fwd) / `GeneralEnable` (rev) | Disc Spreaders 1&2 shut down | running/rotsen/iso | `SFCRunFwd`/`SFCRunRev` |
| 18 | Shredder (`ShredderControlInst1`) | Discharge Conv (`MotorStarterInst9`) enabled | Feed Conv shut down | via FB | via FB |
| 19 | Cyclone Filter (`FilterUnitInst1`) | runs with plant | `FansShutdownReady`/Shredder shut down | `CycloneDust*`; fault = NOT `SysOk` | `CycloneDustFilterStart/Reset`; fan time |
| 20 | Discharge Conv (`MotorStarterInst9`) | `PlantControl.GeneralEnable` | Shredder shut down | running/iso | `SDCStart` |

*(Enable-source/shutdown links transcribe the register's equipment-inventory table; the exact
neighbour instance for each was cross-checked against the resolved instance-DB names in §9. Direction
rationale provisional — Q-02.)*

---

## 9. Tag status

Classification is root-level (does the tag/DB root resolve in the `ae0414a` export?), matching what
`preflight` checks; member existence *within* an existing DB is TIA's compile-time check (noted where
assumed). Established by grep/read (Release `converter` binary absent, §Provenance).

| Group | Roots | Status |
|---|---|---|
| Global DBs | `PlantControl`, `HMIControlSignals`, `DiscreteInputs`, `DiscreteOutputs`, `InterlockData`, `ProcessTimings` | **exists** (read directly) |
| Equipment FB types (8) | `MotorStarter`, `MotorVSDSystem`, `MotorFwdRevSystem`, `AirStarSystem`, `FilterUnitSystem`, `EquipmentControlSystem`, `TomraControlSystem`, `ShredderControlSystem` | **exists** |
| Instance DBs (20) | `MotorStarterInst1..9`, `MotorVSDInst1..3`, `FilterUnitInst1..3`, `AirStarInst1`, `ECSControlInst1`, `TomraControlInst1`, `MotorFwdRevInst1`, `ShredderControlInst1` | **exists** (grep-verified: e.g. `MotorVSDInst2.ir`→`MotorVSDInst2`, `MotorStarterInst81.ir`→`MotorStarterInst8`) |
| Optical-sorter comms words | `Tag_45`…`Tag_54` | **given-boundary** (Q-06 — in-scope/content unclear) |
| **The target block** | `PlantAutoControl` | **proposed** — created by this design; not in the working export until a fresh one is taken |

**Member-level assumptions (TIA compile-time checks, not root-level):** all `DiscreteInputs.*` /
`DiscreteOutputs.*` / `PlantControl.*` / `InterlockData.*` members named in §7/§8 are assumed present
within their (existing) DBs — the register grep-verified them and the run-state/interlock/timing
members were confirmed by direct read of `Control.ir`/`PLC.ir`/`Timings.ir`. The equipment UDT
members (`IO.AutoStartSignal`, `IO.UPSEnable`, `IO.ShutdownComplete`, `IO.Reverse`, …) are given-
boundary; `MotorStarter.IO` (`MotorIOSet`) was read in full, the other 7 FB interfaces are verified
at the compile gate. **No `proposed` result blocks this stage** — it designs against gaps.

---

## 10. Open questions

**Carried from the register (never silently resolved):**

- **Q-01** — `PlantControl.Status` encoding (`>=1` running, `-1` controlled shutdown, `0` idle);
  finer sub-states? Needs plant-sequencer spec.
- **Q-02** — physical material-flow direction (validates the "downstream" reading of §4/§8 enable
  edges). Logic correct regardless; rationale inferred.
- **Q-03** — `JOB9001` third-party interlock process meaning (REQ-014).
- **Q-04** — auto-pre-start keyed off the link conveyor: designated first machine, or incidental?
  (REQ-015).
- **Q-05** — feed-conveyor reversal purpose (anti-blockage inferred) (REQ-016).
- **Q-06** — optical-sorter data-word interface (`Tag_45…Tag_54`): in scope for regeneration? what
  do the words carry? (REQ-017) — **blocks committing the comms-word wiring; treated as provisional.**
- **Q-07** — cross-referenced hand-intervention flags: nets 10 (Sorter Conv VSD) and 11 (Optical
  Sorter) gate their own auto-shutdown on a *different* machine's `HandIntervention`. Register flags
  this as a likely as-built copy/paste defect, not a requirement (REQ-006 states the intended per-
  machine behaviour). **Owner ruling needed: deliberate grouping, or a defect to correct in the
  regeneration?** This directly affects how nets 10/11 are coded — must be resolved before Build.
- **Q-08** — air-separator VSD alternative permissive process meaning (REQ-021).

**NEW (this design):**

- **Q-09 (NEW)** — nothing further; reserved (kept numbering aligned with Q-11/Q-12/Q-13 below being
  the substantive new ones). *(No new question at this slot.)*
- **Q-10 (NEW)** — `PlantAutoControl`/`PlantAutoControl` lacks doc-06's `FC_` prefix (C-003) and OB1 +
  the Map FCs are not in the given export. Keep the frozen block name and confirm OB1 wiring
  (`FC Inputs` → `PlantAutoControl` → `FC Outputs`, C-110), or rename? (§1/§5).
- **Q-11 (NEW)** — C-403 startup reset for the S/R state this block/its FBs hold
  (`PlantControl.AutoPreStart`, `MotorStarter.IO.StopMotor`, …). OB100 is out of this regeneration's
  scope and the boundary has no `DB_PLC`. Confirm a startup-reset block exists elsewhere, or record
  an owner waiver (§3).
- **Q-12 (NEW)** — C-117 stopped-before-reverse for the Feed Conveyor (net 17, REQ-016): is the
  not-running interlock enforced inside `MotorFwdRevSystem` (C-117's FB-internal allowance), or must
  `PlantAutoControl` add the term? Verify against the FB before coding (§4/§6).
- **Q-13 (NEW)** — `chained-permissive-enable` is admitted with criterion 3 (a genuinely-blind
  drafted instance) recorded as still owed. This Build stage is a candidate to close that gap;
  gate-1 sign-off should note the pattern-admission caveat (§6).

---

## Gate-1 sign-off

**APPROVED for Build — orchestrator (Claude Code), 2026-07-20.** Per the S6-Killer-Plan owner
decision (*run straight through*), gate 1 is approved by the **orchestrator, NOT an
answer-key-independent engineer** — this sign-off is **answer-key-biased** and Phase-3 grading weights
it accordingly. `gen-alarm-design` is out of scope for this validation.

**Gate-1 rulings on the four Build-blocking questions** (ruled from the register + conventions, not the
answer key):
- **Q-06 (comms-word scope) → OUT of scope for `PlantAutoControl`.** REQ-017's requirement is the
  sorter's hardwire ready/running/fault signals + run command; the `Tag_45…Tag_54` process-data-word
  marshalling belongs to a separate comms block, not this PlantAutoControl layer. Code net 11 with the
  hardwire signals + `TomraRun` only; do not wire the comms words.
- **Q-07 (cross-referenced hand flags) → implement REQ-006 CORRECTLY, per-machine, on ALL nets
  (incl. 10/11).** Each net gates its own auto-shutdown on its OWN instance's `HandIntervention`. The
  answer key's nets 10/11 use a neighbour's flag (register-flagged likely copy/paste defect); the
  regeneration deliberately implements the requirement — an intended correctness **IMPROVEMENT** for
  Phase 3 to grade against REQ-006.
- **Q-11 (C-403 startup reset / OB100) → OUT of scope for this block.** OB100/startup-reset is a
  separate project-level block; `PlantAutoControl` compiles standalone. A known project-level gap, not
  this regeneration's responsibility.
- **Q-12 (C-117 stopped-before-reverse, feed conveyor) → Build resolves by reading
  `MotorFwdRevSystem.ir`.** If the FB enforces the reversal interlock internally, rely on it; else add
  the not-running term in `PlantAutoControl` per C-117.
- **Q-10 (block name) → keep `PlantAutoControl`** (C-004 regeneration; the missing `FC_` prefix is a
  convention-review note, not a Build blocker).

Carried register questions Q-01…Q-05, Q-08 stay open (inferred-intent rationale; they do not block
Build). **Design reviewed and approved for build:** _orchestrator, 2026-07-20 (biased — see above)_.

*Block count: 1 created FC (`PlantAutoControl`) + 8 reused FB types + 20 reused instance DBs + 6
reused global DBs. Freeform ≈ 5% (does not trip >20%). REQ coverage: 22/22 (REQ-022 out-of-scope by
design). Open questions: 8 carried + 4 substantive NEW.*
