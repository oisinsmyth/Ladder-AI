# PlantAutoControl-bench — Requirements Register

The numbered functional-requirements register for `PlantAutoControl` (the automatic per-equipment
control layer of a shredding / material-recovery plant). Produced by
`docs/15-generation-pipeline.md`'s `gen-spec-analysis` stage (performed manually — the skill does
not exist yet) as **Phase 1 (Examiner)** of the S6-Killer-Plan answer-key validation
(`docs/notes/S6-Killer-Plan.md`). This is the input artifact the later blind **Candidate** will
regenerate `PlantAutoControl` from; REQ IDs below are stable forever.

## Provenance

- **Produced:** 2026-07-20, `manual:gen-spec-analysis`.
- **Reverse-derived, NO independent source document.** This register was derived **entirely from
  the as-built `PlantAutoControl` logic** (`docs/evidence/PlantAutoControl-answerkey/PlantAutoControl.ir`,
  the sealed answer key). There is **no functional description, spec sheet, or other independent
  source** behind it — unlike `gen/test-project001/requirements.md`, which had two site
  documents. This makes the register's own blindness **imperfect by construction**: every
  requirement here was read back out of code. The defense against circularity is therefore **not**
  source-derivation but the **abstraction discipline** in the S6-Killer-Plan protocol — each item
  is stated as observable plant behaviour (WHAT/WHY), never as the ladder structure that encodes it
  (HOW). The judgment calls that discipline forced are logged, honestly and specifically, in
  `docs/evidence/PlantAutoControl-bench-derivation-notes.md`.
- **Level-1 scope — the interface is GIVEN, not under test.** The plant global DBs
  (`PlantControl`, `HMIControlSignals`, `DiscreteInputs`, `DiscreteOutputs`, `InterlockData`,
  `ProcessTimings`) and the equipment instance DBs / their interface UDTs
  (`MotorStarterInst*`, `MotorVSDInst*`, `FilterUnitInst*`, `AirStarInst1`, `ECSControlInst1`,
  `TomraControlInst1`, `MotorFwdRevInst1`, `ShredderControlInst1`, and the 8 equipment FB types)
  are the **fixed boundary handed to the Candidate**. Interface *design* is **not** under test;
  this register names the boundary as fixed and does not re-specify it. The global-DB members
  resolve in `ir/PlantAutoControl-bench/`; the instance/interface members are deferred to Phase 2 and
  are treated as the existing equipment interface (marked `given (Phase-2 boundary)`).
- **Scope of THIS block.** `PlantAutoControl` is **only the per-equipment automatic-control layer**:
  for each piece of equipment it maps field feedback in, computes the automatic start/stop/interlock
  permissives from the plant run state, drives the physical run output, and calls the equipment's
  own control FB. It does **not** contain, and this register does **not** specify: the plant master
  start/stop sequencer, the value/computation of the plant run-state word (`PlantControl.Status`),
  the pre-start (siren) timer itself, the E-stop / safety circuit, or any equipment's internal
  sequence (e.g. the shredder's overcurrent/reversal logic lives inside `ShredderControlSystem`,
  not here). Those are other blocks or the hardwired safety circuit — out of scope for regeneration.
- **Green / sanitized.** Every equipment, tag, DB, and instance name here is an invented name from
  the S6-Killer-Plan sanitization map (`docs/evidence/PlantAutoControl-answerkey/`,
  `ir/PlantAutoControl-bench/`). No real site, site, job, or model identity appears. Work stays
  entirely in the invented-name namespace.
- **Tag-status verification corpus:** `ir/PlantAutoControl-bench/` (the six given global DBs). Every
  `exists` mark below was grep-verified against those files when this register was written.
- **Safety:** No network of `PlantAutoControl` reads, writes, or references any F-block,
  F-runtime, safety-program content, or E-stop signal. The E-stop feedback members that exist in
  the given `DiscreteInputs`/`PlantControl` DBs are **not touched** by this block. Hard rule 2 was
  not triggered.

## Format

Mirrors `gen/test-project001/requirements.md` (the defining instance of the `requirements.md`
format). Same rules apply:

- **IDs:** `REQ-nnn`, source order, **stable forever**. Never renumbered or reused.
- **Classes** (one per REQ): `control` (sequencing/interlocks/drives), `alarm`
  (fault-annunciation causes), `HMI` (operator commands/indications), `mode` (auto/hand/enable
  semantics), `timing` (named delays/setpoints as the ask itself), `out-of-scope` (hardwired/safety
  or other-block material, recorded so nobody expects PLC logic for it here).
- **Sources:** every REQ cites its as-built origin. Because this is a reverse-derivation, the
  citation is the answer-key network(s) it was read from — e.g. *"reverse-derived from
  PlantAutoControl network 18 (Shredder Automatic Control)"*. **Network numbers appear ONLY in the
  Source field, never in requirement Text** (the anti-leakage rule).
- **WHAT, never HOW:** the Text records observable plant behaviour. It never names an instruction,
  a network, a coil/contact, an edge-memory, or a rung structure. The given tag / DB-member /
  interface-member names are the boundary, not leakage, and may appear.
- **Tag status:** `exists` (grep-verified in `ir/PlantAutoControl-bench/`), `given (Phase-2 boundary)`
  (equipment instance / interface UDT member, deferred to Phase 2), or `proposed` (a named gap).
- **Open questions:** `Q-nn`, own section; never silently resolved.

## Requirements

### Common automatic-control contract (applies to every equipment)

### REQ-001 — Automatic control of the plant equipment train
- **Text:** The plant equipment train — twenty named pieces of equipment (feed and transfer
  conveyors, the shredder, disc spreaders, an overband magnet, a drum separator, an optical sorter,
  a metal-collection conveyor, discharge conveyors, dust filter units, a cyclone filter unit, and
  an air-separator VSD) — is run under automatic control as one coordinated system, commanded by
  the plant run state rather than by an individual operator run command per machine.
- **Class:** control
- **Source:** reverse-derived from PlantAutoControl networks 1–20 (the per-equipment automatic
  control networks); equipment names from the network titles.
- **Notes:** Equipment inventory below lists each machine, its instance, and its interlock links.

### REQ-002 — Plant running state commands equipment to start
- **Text:** While the plant is in its running state, each piece of equipment is commanded to start
  and run automatically (subject to its own permissives, REQ-004/014).
- **Class:** control
- **Source:** reverse-derived from the auto-start condition common to PlantAutoControl networks 1–20.
- **Notes:** The plant run state is carried by the given `PlantControl.Status` word; the running
  state corresponds to `Status >= 1` and the controlled-shutdown state to `Status = -1`
  (interpretation reverse-derived — see Q-01). This block reads `Status`; it does not compute it.

### REQ-003 — Sequential (cascaded) controlled shutdown
- **Text:** On a plant controlled shutdown, equipment does not all stop at once. Each machine keeps
  running until the specific neighbouring machine in its shutdown chain has signalled that it has
  completed its own shutdown, and only then stops — a staged shutdown that propagates through the
  train (so material already on the line is cleared in order rather than stranded).
- **Class:** control
- **Source:** reverse-derived from the shutdown condition common to PlantAutoControl networks 1–20
  (each network holds run during the shutdown state until a named neighbour's shutdown-complete).
- **Notes:** The per-machine "hold until X has finished shutting down" links are in the equipment
  interlock table below. This is distinct from an E-stop (out of scope, REQ-020).

### REQ-004 — Downstream-ready start interlock (material-handling sequence)
- **Text:** A machine is permitted to start automatically only while the machine(s) receiving its
  material downstream are already enabled / up to speed. Receiving equipment must be running before
  the equipment feeding it is allowed to start, so material is never delivered onto a stopped
  machine.
- **Class:** control
- **Source:** reverse-derived from the start permissive common to PlantAutoControl networks 1–20
  (each machine's auto-start is gated by a named neighbour's up-to-speed enable).
- **Notes:** The per-machine permissive links are in the equipment interlock table. Cascade
  direction (which machine is "downstream") is reverse-derived from the dependency graph — Q-02.

### REQ-005 — Pre-start warning gate
- **Text:** No equipment starts automatically until the plant pre-start warning phase has
  completed. Every machine waits on the plant pre-start-complete condition before its automatic run
  is permitted.
- **Class:** control
- **Source:** reverse-derived from the pre-start-done condition fed to every machine in
  PlantAutoControl networks 1–20.
- **Notes:** `PlantControl.PreStartComplete` (exists). The pre-start timer/sequence itself is
  another block, not this one.

### REQ-006 — Hand (manual) intervention suppresses automatic shutdown
- **Text:** While a machine is under hand / manual intervention, the automatic controlled-shutdown
  request for that machine is suppressed — the operator's hand control is not overridden by the
  plant shutdown cascade.
- **Class:** mode
- **Source:** reverse-derived from the shutdown condition common to PlantAutoControl networks 1–20
  (auto shutdown gated by "not hand intervention").
- **Notes:** Uses each machine's `HandIntervention` interface member. Two networks reference a
  *different* machine's hand flag than their own — recorded as a possible as-built quirk, not a
  requirement — see Q-07 and the derivation notes.

### REQ-007 — Single global fault reset
- **Text:** One operator system-reset command clears faults across all equipment at once; every
  machine's fault reset is driven from the same plant-wide reset.
- **Class:** HMI
- **Source:** reverse-derived from the fault-reset condition common to PlantAutoControl networks 1–20.
- **Notes:** `HMIControlSignals.SystemReset` (exists). Filter units and the ECS additionally echo
  this reset to a physical reset output (REQ-018/019).

### REQ-008 — Computed run command drives the physical output
- **Text:** Each machine's resulting run command (from its control FB) drives the corresponding
  physical motor/starter start output.
- **Class:** control
- **Source:** reverse-derived from the run-to-output mapping in PlantAutoControl networks 2, 3, 4,
  7, 8, 9, 11, 12, 13, 14, 15, 16, 17, 19, 20.
- **Notes:** Physical outputs are `DiscreteOutputs.*Start` / `*RunFwd` / `*RunRev` / `TomraRun`
  (all exist). The incline feed VSD, air-separator VSD, sorter-conveyor VSD and shredder are
  commanded through their FB/VSD interface rather than a discrete start bit.

### REQ-009 — Running confirmation from field feedback
- **Text:** Each machine's running status is confirmed from its field running feedback signal (and,
  for belt conveyors, from a motion/rotation sensor — REQ-011).
- **Class:** control
- **Source:** reverse-derived from the running-feedback mapping in PlantAutoControl networks 2, 3,
  4, 7, 8, 12, 13, 14, 15, 16, 17, 20.
- **Notes:** Running feedbacks are `DiscreteInputs.*Running` / `*Op` (exist).

### REQ-010 — Isolator inhibits the motor
- **Text:** Each motor is inhibited from running while its local isolator feedback indicates the
  machine is isolated.
- **Class:** control
- **Source:** reverse-derived from the isolator/inhibit mapping in PlantAutoControl networks 1, 2,
  6, 7, 8, 10, 12, 13, 14, 15, 16, 17, 20.
- **Notes:** Isolator feedbacks are `DiscreteInputs.*IsoFB` (exist).

### REQ-011 — Belt-conveyor motion confirmation, operator-bypassable per conveyor
- **Text:** Belt conveyors require a motion/rotation sensor to confirm the belt is actually turning,
  in addition to the run feedback, before the belt counts as running. Each such conveyor's
  rotation-sensor check can be individually bypassed by the operator (for a failed sensor or
  commissioning) so the conveyor can still be run on its run feedback alone.
- **Class:** HMI
- **Source:** reverse-derived from the running-feedback confirmation in PlantAutoControl networks 2,
  7, 13, 14, 17, 20 (and the discharge-conveyor VSD, network 6 — REQ-012).
- **Notes:** Rotation sensors `DiscreteInputs.*RotSen`; per-conveyor bypass flags
  `HMIControlSignals.Bypass*RotSen` (all exist). Machines without a rotation sensor (overband
  magnet, drum separator, disc spreaders, dust filter units) confirm on run feedback only.

### REQ-012 — Discharge-conveyor VSD running feedback derived from its rotation sensor
- **Text:** The discharge-conveyor VSD treats its rotation sensor as the source of its "running
  forward" confirmation (motion sensed = running), unless that sensor is bypassed, in which case it
  is not used.
- **Class:** control
- **Source:** reverse-derived from PlantAutoControl network 6 (Discharge Conveyor VSD Automatic
  Control).
- **Notes:** `DiscreteInputs.AirStarDCRotSen`, `HMIControlSignals.BypassAirStarDCRotSen` (exist).

### REQ-013 — Sub-system enable gates
- **Text:** Some machines only start when their plant sub-system is separately enabled: the
  metal-collection conveyor is gated by the magnet-circuit enable; the drum separator by the
  drum-separator enable; and the ejected-material conveyor, residual/discharge conveyors and the
  feed-conveyor reverse path by the general enable. A machine whose sub-system enable is off does
  not start even when the plant is running.
- **Class:** mode
- **Source:** reverse-derived from PlantAutoControl networks 7 (magnet), 12 (drums), 13, 20, 17
  (general).
- **Notes:** `PlantControl.MagEnable`, `PlantControl.DrumsEnable`, `PlantControl.GeneralEnable`
  (all exist). The drum separator also holds through shutdown on `PlantControl.DrumShutdownReady`;
  filter units on `PlantControl.FansShutdownReady` (exist).

### REQ-014 — Third-party / upstream-plant interlock on the incline feed and link conveyor
- **Text:** The incline feed conveyor and the link conveyor are additionally gated by an interlock
  with the adjacent third-party plant. On a controlled shutdown they keep running until that
  third-party plant signals its own shutdown is complete.
- **Class:** control
- **Source:** reverse-derived from PlantAutoControl networks 1 (Incline Feed Conveyor) and 2 (Link
  Conveyor Automatic Control).
- **Notes:** `InterlockData.JOB9001Interlock`, `InterlockData.JOB9001ShutdownComplete` (exist).
  Process meaning of the third-party handshake is reverse-derived — Q-03.

### REQ-015 — Automatic pre-start request
- **Text:** When automatic start is first requested (link conveyor commanded to auto-start with the
  third-party interlock present and the plant running), the plant pre-start warning is requested
  automatically, once per start; the request is cleared again once the pre-start output has become
  active.
- **Class:** control
- **Source:** reverse-derived from PlantAutoControl network 2 (the auto-pre-start set/reset on the
  link-conveyor auto-start).
- **Notes:** `PlantControl.AutoPreStart`, `DiscreteOutputs.RunPreStart` (exist). Why the request is
  keyed off the link conveyor specifically (its role as the designated first machine) is
  reverse-derived — Q-04.

### REQ-016 — Shredder feed conveyor reverses to control feed to the shredder
- **Text:** The shredder feed conveyor runs in reverse to back material away from the shredder when
  the shredder is not ready to receive feed, and runs forward when the shredder is ready to accept
  material. The direction only changes while the conveyor is already running automatically and is
  not under hand control.
- **Class:** control
- **Source:** reverse-derived from PlantAutoControl network 17 (Feed Conveyor Automatic Control).
- **Notes:** Reverse is driven by the shredder's readiness-to-receive (its upstream-enable);
  forward run when ready, reverse (on the general enable) when not. `DiscreteOutputs.SFCRunFwd`,
  `SFCRunRev` (exist). Anti-blockage interpretation is reverse-derived — Q-05.

### REQ-017 — Optical sorter integration and readiness interlock
- **Text:** The optical sorter's hardwired ready, running, and communications-fault signals are
  brought into its control, and its run command is driven to the sorter. The sorter-feed conveyor
  (VSD) is only permitted to start while the optical sorter is ready and not faulted.
- **Class:** control
- **Source:** reverse-derived from PlantAutoControl networks 11 (Optical Sorter) and 10 (Sorter
  Conveyor VSD Automatic Control).
- **Notes:** `DiscreteInputs.TomraReady`, `TomraRunning`, `TomraComFlt`, `DiscreteOutputs.TomraRun`
  (exist). The sorter also exchanges process-data words with the plant — Q-06.

### REQ-018 — Equipment Control System (ECS) integration
- **Text:** The Equipment Control System unit is integrated as one of the automatically controlled
  machines: its fault, mode (auto/hand/setting), ready, running, feeder/charge and rotor/conveyor
  status signals are brought in, and its automatic-start and reset outputs are driven. It
  participates in the plant start/shutdown cascade like the other equipment.
- **Class:** control
- **Source:** reverse-derived from PlantAutoControl network 9 (Equipment Control System Automatic
  Control).
- **Notes:** ECS signals `DiscreteInputs.ECS*` and outputs `DiscreteOutputs.ECSAutoStart`,
  `ECSReset` (exist).

### REQ-019 — Filter-unit feedback mapping and reset
- **Text:** Each dust filter unit and the cyclone filter unit brings in its remote-operational,
  running, and fault feedbacks, and drives a fault-reset output back to the unit. The cyclone
  filter unit's fault is taken from the inverse of its system-OK signal.
- **Class:** control
- **Source:** reverse-derived from PlantAutoControl networks 3, 4 (Dust Filter Units 1/2) and 19
  (Cyclone Filter Unit Control).
- **Notes:** `DiscreteInputs.FilterUnit1/2Ready/Op/Flt`, `CycloneDustRemOp`,
  `CycloneDustAutoRunning/Stop`, `CycloneDustSysOk`; outputs `FilterUnit1/2Start/Reset`,
  `CycloneDustFilterStart/Reset` (exist).

### REQ-020 — Filter-unit fan start-up time
- **Text:** The dust filter units and the cyclone filter unit use a configurable fan start-up
  (up-to-speed) time before they are treated as enabling downstream equipment; the commissioning
  value is 10 s.
- **Class:** timing
- **Source:** reverse-derived from PlantAutoControl networks 3, 4, 19 (fan start time applied to
  each filter unit's enable-time input).
- **Notes:** `ProcessTimings.NormalFanStartTime` (exists, start value 10.0). This is the only
  `ProcessTimings` value this block uses; the other timing members belong to other blocks and are
  out of scope here.

### REQ-021 — Air-separator VSD alternative start permissive
- **Text:** The air-separator VSD is permitted to start either when the discharge-conveyor VSD and
  both dust filter units are enabled, or (alternatively) when the discharge-conveyor VSD is enabled
  and a third-party link-out signal is present.
- **Class:** control
- **Source:** reverse-derived from PlantAutoControl network 5 (VSD Motor Automatic Control).
- **Notes:** `InterlockData.LinkOut3rdParty` (exists, start value TRUE). Process meaning of the
  alternative path is reverse-derived — Q-08.

### REQ-022 — Health / safety supervised outside this layer
- **Text:** This automatic-control layer treats every machine as system-healthy (it asserts the
  healthy input true for each machine). Plant health, the E-stop function, and safety interlocking
  are handled by the hardwired safety circuit and other blocks — no PLC logic for them belongs in
  this block.
- **Class:** out-of-scope
- **Source:** reverse-derived from the constant system-healthy assignment in PlantAutoControl
  networks 1–20; recorded so no stage expects health/safety logic here.
- **Notes:** Hard rule 2 / hardwired safety circuit. The E-stop feedback members that exist in the
  given DBs are deliberately untouched by this block.

## Sequence classification (C-113 memory test)

Per docs/06 C-113, each behaviour is classified by the memory test — *does the logic need to
remember what phase it is in to know what to do next?* This classifies the requirement, not a
design.

| Behaviour | Memory test | Paradigm |
|---|---|---|
| Per-equipment auto-start / stop / interlock permissives (REQ-002, 004, 005, 009–014, 017–022) | **No** — each machine's enable/inhibit/start permissive is a function of current plant state, current neighbour-enable signals, and current feedback; no phase memory is needed *in this block* | Combinational (chained conditions per machine; the stateful sequencing lives in the plant sequencer and the equipment FBs, not here) |
| Cascaded controlled shutdown (REQ-003) | **No, within this block** — each machine's "keep running until neighbour X has shut down" is a current-signal test against a neighbour's shutdown-complete; the *ordering* emerges from the graph, not from a step counter here | Combinational (the shutdown state itself is owned by the plant sequencer) |
| Shredder feed-conveyor direction (REQ-016) | **Yes (narrowly)** — reverse is *latched* on the shredder-not-ready condition and released on shredder-ready, so the conveyor remembers it was reversing until the release condition | Latched state (set/reset on the shredder-readiness edge), the one small stateful element in this block |
| Automatic pre-start request (REQ-015) | **Yes (one-shot)** — the request is a single-shot on first auto-start, remembered until the pre-start output clears it | Edge-triggered one-shot |

Note: the block as a whole is a **combinational interlock/mapping layer** over stateful equipment
FBs and a stateful plant sequencer that live elsewhere. The only memory it holds itself is the
feed-conveyor reverse latch (REQ-016) and the pre-start one-shot (REQ-015).

## Equipment inventory

### Global-DB signals and settings (resolve in `ir/PlantAutoControl-bench/`)

| Group | Members used by this block | Status |
|---|---|---|
| Plant run state / enables (`PlantControl`) | `Status`, `PreStartComplete`, `GeneralEnable`, `DrumsEnable`, `MagEnable`, `DrumShutdownReady`, `FansShutdownReady`, `AutoPreStart`, `PositiveEdgeArray[4]` | exists |
| HMI signals (`HMIControlSignals`) | `SystemReset`; per-conveyor rotation bypasses `BypassHFLCRotSen`, `BypassFMCCRotSen`, `BypassOSECRotSen`, `BypassOSRCRotSen`, `BypassSFCRotSen`, `BypassSDCRotSen`, `BypassAirStarDCRotSen` | exists |
| Field inputs (`DiscreteInputs`) | running feedbacks `*Running`/`*Op`; rotation sensors `*RotSen`; isolators `*IsoFB`; filter `FilterUnit1/2Ready/Op/Flt`; sorter `TomraReady/Running/ComFlt`; ECS `ECS*`; cyclone `CycloneDustRemOp`/`CycloneDustAutoRunning/Stop`/`CycloneDustSysOk` | exists |
| Field outputs (`DiscreteOutputs`) | `HFLCStart`, `FMCCStart`, `OverbandMagStart`, `OSDrumSepStart`, `OSECStart`, `OSRCStart`, `MotorStarterInst1/2Start`, `SFCRunFwd`, `SFCRunRev`, `SDCStart`, `RunPreStart`, `FilterUnit1/2Start`, `FilterUnit1/2Reset`, `ECSAutoStart`, `ECSReset`, `CycloneDustFilterStart`, `CycloneDustFilterReset`, `TomraRun` | exists |
| Third-party interlock (`InterlockData`) | `JOB9001Interlock`, `JOB9001ShutdownComplete`, `LinkOut3rdParty` | exists |
| Timings (`ProcessTimings`) | `NormalFanStartTime` = 10.0 (fan start-up time, REQ-020) | exists |
| Optical-sorter comms words | `Tag_45`…`Tag_54` (8 in / 2 out data words to the sorter FB) | given-boundary (Q-06) |

### Equipment instances and interlock links (given Phase-2 boundary)

Each machine is an instance DB of one of the 8 given equipment FB types; the instance DBs and their
interface UDT members (`.IO.*` / `.Inputs.*` / `.Outputs.*`: `UPSEnable`, `EnableUPS`, `Run`,
`RunningFB`, `InhibitMotor`, `PreStartDone`, `SystemHealthy`, `AutoStartSignal`, `Shutdown`,
`ShutdownComplete`, `FaultReset`, `HandIntervention`, `Reverse`, `EnableUPSTime`, …) are the
**given, deferred (Phase-2) boundary** — treated as the existing equipment interface, not
grep-verified here. "Starts after" = the neighbour whose up-to-speed enable is this machine's start
permissive (REQ-004). "Holds through shutdown until" = the neighbour whose shutdown-complete
releases this machine (REQ-003).

| # | Equipment (title) | Instance | FB type | Starts after (permissive) | Holds through shutdown until | Extra gate |
|---|---|---|---|---|---|---|
| 1 | Incline Feed Conveyor | `MotorVSDInst2` | MotorVSDSystem | Air-separator VSD enabled | Link Conveyor shut down | JOB9001 interlock (shutdown) |
| 2 | Link Conveyor | `MotorStarterInst4` | MotorStarter | Incline Feed Conveyor enabled | Third-party plant shutdown complete | JOB9001 interlock (both states); triggers auto-pre-start |
| 3 | Dust Filter Unit 1 | `FilterUnitInst2` | FilterUnitSystem | Sorter Conveyor VSD enabled | Fans-shutdown-ready / Air-separator VSD shut down | fan start time |
| 4 | Dust Filter Unit 2 | `FilterUnitInst3` | FilterUnitSystem | Sorter Conveyor VSD enabled | Fans-shutdown-ready / Air-separator VSD shut down | fan start time |
| 5 | Air-Separator VSD | `AirStarInst1` | AirStarSystem | Discharge-Conveyor VSD + both Dust Filters enabled (or Discharge-Conveyor VSD + third-party link-out) | Incline Feed Conveyor shut down | — |
| 6 | Discharge Conveyor VSD | `MotorVSDInst1` | MotorVSDSystem | Overband Magnet + ECS enabled | Air-separator VSD shut down | rotation-sensor running FB |
| 7 | Metal Collection Conveyor | `MotorStarterInst3` | MotorStarter | (magnet enable) | Overband Magnet shut down | MagEnable |
| 8 | Overband Magnet | `MotorStarterInst8` | MotorStarter | Metal Collection Conveyor enabled | Discharge Conveyor VSD shut down | — |
| 9 | Equipment Control System | `ECSControlInst1` | EquipmentControlSystem | Sorter Conveyor VSD enabled | Discharge Conveyor VSD shut down | ECS mode/fault signals |
| 10 | Sorter Conveyor VSD | `MotorVSDInst3` | MotorVSDSystem | Optical Sorter ready & not faulted + Ejected & Residual conveyors enabled | ECS shut down | — |
| 11 | Optical Sorter | `TomraControlInst1` | TomraControlSystem | Ejected & Residual conveyors enabled | Sorter Conveyor VSD shut down | comms words |
| 12 | Drum Separator | `MotorStarterInst5` | MotorStarter | (drums enable) | Drum-shutdown-ready | DrumsEnable |
| 13 | Ejected Material Conveyor | `MotorStarterInst6` | MotorStarter | (general enable) | Optical Sorter shut down | GeneralEnable |
| 14 | Residual Material Conveyor | `MotorStarterInst7` | MotorStarter | Disc Spreaders 1 & 2 enabled | Optical Sorter shut down | — |
| 15 | Disc Spreader Unit 1 | `MotorStarterInst1` | MotorStarter | Feed Conveyor enabled | Residual Material Conveyor shut down | — |
| 16 | Disc Spreader Unit 2 | `MotorStarterInst2` | MotorStarter | Feed Conveyor enabled | Residual Material Conveyor shut down | — |
| 17 | Feed Conveyor (shredder feed) | `MotorFwdRevInst1` | MotorFwdRevSystem | Shredder ready (fwd) / general enable (rev) | Disc Spreaders 1 & 2 shut down | reversal per REQ-016 |
| 18 | Shredder | `ShredderControlInst1` | ShredderControlSystem | Discharge Conveyor enabled | Feed Conveyor shut down | — |
| 19 | Cyclone Filter Unit | `FilterUnitInst1` | FilterUnitSystem | (runs with plant) | Fans-shutdown-ready / Shredder shut down | fan start time |
| 20 | Discharge Conveyor | `MotorStarterInst9` | MotorStarter | (general enable) | Shredder shut down | GeneralEnable |

(The eight FB types — `MotorStarter`, `MotorVSDSystem`, `FilterUnitSystem`, `AirStarSystem`,
`EquipmentControlSystem`, `TomraControlSystem`, `MotorFwdRevSystem`, `ShredderControlSystem` — and
all 20 instance DBs are given library blocks, deferred to Phase 2. Their internal sequences are out
of scope for this block, REQ-001 provenance.)

## Open questions

Never silently resolved; resolution is a recorded owner answer noted at the question. Because the
whole register is reverse-derived, these mark the places where the as-built logic is faithful but
its *process intent* is genuinely inferred and could be wrong.

- **Q-01 — Plant run-state (`Status`) encoding.** The requirements read `Status >= 1` as the
  running state and `Status = -1` as the controlled-shutdown state (0 presumably stopped/idle).
  This is reverse-derived. Are there finer sub-states within `>= 1` (e.g. staged start phases) that
  this block does not distinguish but another block does? Needs the plant-sequencer spec to confirm.
- **Q-02 — Physical material-flow direction.** The start "starts-after" links and the shutdown
  "holds-until" links were read from the dependency graph and interpreted as a downstream-first
  start / upstream-first stop cascade. Confirmation of the actual material-flow direction through
  the plant would validate the "downstream" reading (the logic is correct regardless; only the
  *rationale* is inferred).
- **Q-03 — Third-party interlock (`JOB9001`) meaning.** Interpreted as a handshake with an adjacent
  third-party plant gating the incline feed / link conveyor. The exact process semantics (what the
  interlock and its shutdown-complete represent) are inferred.
- **Q-04 — Auto-pre-start trigger point.** The automatic pre-start request (REQ-015) is keyed off
  the link conveyor's first auto-start. Is the link conveyor the designated "first machine" for
  this by process design, or is that placement incidental? Inferred.
- **Q-05 — Feed-conveyor reversal purpose.** REQ-016 reads the shredder-feed reversal as
  anti-blockage feed control (back off when the shredder cannot accept feed, forward when it can).
  The direction logic is faithful; the *purpose* is inferred.
- **Q-06 — Optical-sorter data-word interface.** The sorter FB is called with 8 input and 2 output
  process-data words (`Tag_45`…`Tag_54`). Whether reconstructing that word interface is in scope
  for regeneration, and what those words carry, is unclear — they read as a given comms buffer, not
  a derivable requirement.
- **Q-07 — Cross-referenced hand-intervention flags.** Two machines (the sorter conveyor VSD and
  the optical sorter) gate their *own* automatic shutdown on a *different* machine's
  hand-intervention flag rather than their own. This looks like an as-built copy/paste quirk rather
  than intended "grouped hand" behaviour. Recorded as a possible defect, not written as a
  requirement (REQ-006 states the intended per-machine behaviour). Needs an owner ruling: deliberate
  grouping, or a defect to correct?
- **Q-08 — Air-separator VSD alternative permissive.** REQ-021's second start path (discharge VSD
  enabled + third-party link-out present, bypassing the dust-filter enables) — its process meaning
  (a degraded/link-out running mode?) is inferred.
