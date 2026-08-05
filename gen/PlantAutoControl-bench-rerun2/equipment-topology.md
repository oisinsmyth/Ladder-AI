# PlantAutoControl-bench-rerun2 — Equipment topology and per-equipment interlocks (rung A)

Produced by `/gen-pid-analysis` (rung A of the structured spec pipeline A→B→C→D).
**Process relations only** — no signals, no IO addresses, no tag names, no booleans, no interface
members. Signals enter at rung C, booleans/interfaces at rung D.

## Provenance

- **Produced:** 2026-08-05, `gen-pid-analysis`, run `PlantAutoControl-bench-rerun2`.
- **Layout / topology source (REQUIRED input):** `gen/PlantAutoControl-bench/requirements.md`
  — specifically its **"Equipment instances and interlock links"** table (the equipment inventory:
  columns *Equipment*, *Instance*, *FB type*, *Starts after (permissive)*, *Holds through shutdown
  until*, *Extra gate*), plus the *Global-DB signals and settings* table for plant-level conditions.
  SHA-256 `c0de9b797b75085b40c167ad9e8b672ae25d48b0e74ed4a4f4f9e157ad46ecba`.
  This inventory is the only layout-like source available. **It is an equipment list with a
  dependency graph, not a P&ID and not a material-flow drawing** — see Q-A03.
- **Equipment class references used:**
  - `references/FilterUnitSystem/reference.md` (v0-derived 2026-08-04), SHA-256 `277c5094…b4b9ded`
  - `references/vsd-motor/reference.md` (v0-derived 2026-08-04), SHA-256 `77a3b52d…1386d6`
  - `references/optical-sorter/reference.md` (v0-derived 2026-08-04), SHA-256 `63884354…94b6b1`
  Only the *Class requirement set (process abstraction)* section of each was consumed. Their
  *As-built provenance* tables are marked rung-C material and were **not** used to derive anything
  below (see the friction note in the run report — those tables sit inside files this rung is
  required to read, so the prohibition is honoured by discipline, not by construction).
- **Scope of this run — six instances only,** as commissioned:
  `FilterUnitInst2`, `FilterUnitInst3`, `FilterUnitInst1`, `MotorVSDInst1`, `MotorVSDInst3`,
  `TomraControlInst1`. Neighbouring machines are *referenced* by relations below but are **not**
  specified here.
- **Question IDs** are rung-prefixed (`Q-Ann`) so downstream rungs can cite them unambiguously;
  this is the contract's `Q-nn` field with a rung prefix.
- **Safety:** no F-block, F-runtime, safety-program or E-stop content was read or referenced. Hard
  rule 2 not triggered.

## Interlock convention (stated once, applied without exception)

Taken verbatim in meaning from the source inventory's own definitions, and applied to every
instance below identically:

1. **Start permissive ("starts after X"):** *this machine may start automatically only while machine
   X has declared itself enabled* (X has run and been confirmed running for its configurable
   up-to-speed time). X's enable is what permits the machine it serves to start.
2. **Shutdown hold ("holds through shutdown until X"):** *on a plant controlled shutdown this
   machine keeps running until X has signalled that its own shutdown is complete, and only then
   stops.*
3. **Class inheritance:** every instance inherits the **complete** requirement set of its class
   reference. Anything the instance has *beyond* that set, or *lacks* from it, is recorded as a
   delta. The class conditions are restated per instance rather than referenced by ID alone, so no
   instance's relation list is complete only by cross-reference.
4. **Plant-wide conditions** (pre-start warning complete, plant running state, plant-wide fault
   reset, sub-system enables) are relations too, and are listed per instance.

**Convention limits, stated here because they bound every relation below:**
the source inventory settles *which* neighbour a relation names. It does **not** settle material
flow direction (Q-A03), nor how multiple hold-conditions combine (Q-A04), nor several instance
specifics called out per instance.

---

## Instance: Dust Filter Unit 1 — `FilterUnitInst2` : FilterUnitSystem

```
Dust Filter Unit 1 (FilterUnitInst2) : FilterUnitSystem
  fed-by                : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  discharges-to         : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  serves (enable-wise)  : Air-Separator VSD (that machine's start permissive names this unit)
  interlock : start requires the Sorter Conveyor VSD to be enabled
  interlock : controlled shutdown holds until the plant reports the filter fans ready to shut down
  interlock : controlled shutdown holds until the Air-Separator VSD reports its shutdown complete
  interlock : (combination of the two hold conditions above is UNSETTLED — Q-A04; both retained)
  interlock : start requires the plant pre-start warning phase to have completed
  interlock : start requires the plant to be in its running state
  interlock : start requires the unit to be in remote (not local) operating mode
  interlock : the unit does not start while it is inhibited (isolated / locked off)
  interlock : the unit does not start while it has an active fault
  interlock : the unit does not start unless it is reported system-healthy
  interlock : while under hand control the unit runs on the operator's hand start command instead
              of the plant's automatic start command
  interlock : while the unit is under hand intervention, the automatic controlled-shutdown request
              for this unit is suppressed
  interlock : one plant-wide operator reset clears this unit's latched faults
  behaviour : the unit declares itself enabled only after it has been confirmed running for a
              configurable fan start-up (up-to-speed) time; commissioning value 10 s
  behaviour : on a controlled shutdown request the unit stops, and declares its shutdown complete
              once a configurable shutdown time has elapsed and it has stopped running
  behaviour : the unit also declares shutdown complete immediately if it is faulted, or if it is
              under hand control and the operator is not calling it to run
  behaviour : a fail-to-run fault is raised and latched if it is commanded but does not report
              running within a configurable fault time
  behaviour : a fail-to-stop fault is raised and latched if it reports running while not commanded
              for that same fault time
  behaviour : a fault reported by the unit itself raises a latched fault
  behaviour : running state is taken from the unit's own running feedback; this class has no
              motion/rotation sensor
  behaviour : running hours are totalised
  behaviour : operator status indication (faulted / stopped / commanded / running / enabled)
  behaviour : operator alarm indication for fail-to-run, fail-to-stop, unit fault, not-in-remote
  delta (+) : the plant-wide fault reset is additionally echoed back to the unit itself as a
              reset command to the equipment (the class only requires that a reset clears the
              latched faults internally)
  delta (?) : the unit's operational-availability report from the field is brought into the control
              layer; the class requirement set does not name such a report — candidate plus-delta,
              unresolved at this rung (Q-A08)
  deltas    : none-further-found — searched: source inventory row 3 (all six columns), source
              REQ-005/006/007/011/013/019/020 text, FilterUnitSystem reference §"Class requirement set"
              and §"Class deltas against the sibling motor classes", source open questions Q-01…Q-08.
              NOT searched (invisible at this rung by construction): the IO table / operator-bypass
              signals — must be re-hunted at rung C (§8 reverse sweep).
  provenance: start permissive + shutdown hold <- inventory row 3 ("Starts after" / "Holds through
              shutdown until" / "Extra gate" columns); plant-level relations <- REQ-002/005/007;
              hand suppression <- REQ-006; fan start-up time <- REQ-020 + inventory row 3 extra
              gate; class behaviours <- FilterUnitSystem reference FU-01…FU-17; reset echo <- REQ-019;
              availability report <- REQ-019.
```

## Instance: Dust Filter Unit 2 — `FilterUnitInst3` : FilterUnitSystem

```
Dust Filter Unit 2 (FilterUnitInst3) : FilterUnitSystem
  fed-by                : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  discharges-to         : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  serves (enable-wise)  : Air-Separator VSD (that machine's start permissive names this unit)
  interlock : start requires the Sorter Conveyor VSD to be enabled
  interlock : controlled shutdown holds until the plant reports the filter fans ready to shut down
  interlock : controlled shutdown holds until the Air-Separator VSD reports its shutdown complete
  interlock : (combination of the two hold conditions above is UNSETTLED — Q-A04; both retained)
  interlock : start requires the plant pre-start warning phase to have completed
  interlock : start requires the plant to be in its running state
  interlock : start requires the unit to be in remote (not local) operating mode
  interlock : the unit does not start while it is inhibited (isolated / locked off)
  interlock : the unit does not start while it has an active fault
  interlock : the unit does not start unless it is reported system-healthy
  interlock : while under hand control the unit runs on the operator's hand start command instead
              of the plant's automatic start command
  interlock : while the unit is under hand intervention, the automatic controlled-shutdown request
              for this unit is suppressed
  interlock : one plant-wide operator reset clears this unit's latched faults
  behaviour : the unit declares itself enabled only after it has been confirmed running for a
              configurable fan start-up (up-to-speed) time; commissioning value 10 s
  behaviour : on a controlled shutdown request the unit stops, and declares its shutdown complete
              once a configurable shutdown time has elapsed and it has stopped running
  behaviour : the unit also declares shutdown complete immediately if it is faulted, or if it is
              under hand control and the operator is not calling it to run
  behaviour : a fail-to-run fault is raised and latched if it is commanded but does not report
              running within a configurable fault time
  behaviour : a fail-to-stop fault is raised and latched if it reports running while not commanded
              for that same fault time
  behaviour : a fault reported by the unit itself raises a latched fault
  behaviour : running state is taken from the unit's own running feedback; this class has no
              motion/rotation sensor
  behaviour : running hours are totalised
  behaviour : operator status indication (faulted / stopped / commanded / running / enabled)
  behaviour : operator alarm indication for fail-to-run, fail-to-stop, unit fault, not-in-remote
  delta (+) : the plant-wide fault reset is additionally echoed back to the unit itself as a
              reset command to the equipment
  delta (?) : the unit's operational-availability report from the field is brought into the control
              layer; not named in the class requirement set — candidate plus-delta (Q-A08)
  deltas    : none-further-found — searched: source inventory row 4 (all six columns), source
              REQ-005/006/007/011/013/019/020 text, FilterUnitSystem reference §"Class requirement set"
              and §"Class deltas", source open questions Q-01…Q-08. NOT searched (invisible here by
              construction): IO table / operator-bypass signals — re-hunt at rung C §8.
  note      : this instance's relation set is identical in every field to Dust Filter Unit 1. The
              source gives no distinguishing datum. Whether the two units are genuinely identical or
              the source has flattened a difference is Q-A09.
  provenance: as Dust Filter Unit 1 but from inventory row 4.
```

## Instance: Cyclone Filter Unit — `FilterUnitInst1` : FilterUnitSystem

```
Cyclone Filter Unit (FilterUnitInst1) : FilterUnitSystem
  fed-by                : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  discharges-to         : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  serves (enable-wise)  : NOT ESTABLISHED — no machine in the inventory names this unit's enable as
                          its start permissive, yet the class makes "declare enabled to permit the
                          machine it serves" a core requirement (Q-A06)
  interlock : start permissive — NOT ESTABLISHED. The source records only "(runs with plant)": no
              neighbouring machine's enable gates this unit's start (Q-A05, blocking). Recorded as
              a candidate MINUS-delta against the class, not as "no permissive".
  interlock : controlled shutdown holds until the plant reports the filter fans ready to shut down
  interlock : controlled shutdown holds until the Shredder reports its shutdown complete
  interlock : (combination of the two hold conditions above is UNSETTLED — Q-A04; both retained)
  interlock : start requires the plant pre-start warning phase to have completed
  interlock : start requires the plant to be in its running state
  interlock : start requires the unit to be in remote (not local) operating mode
  interlock : the unit does not start while it is inhibited (isolated / locked off)
  interlock : the unit does not start while it has an active fault
  interlock : the unit does not start unless it is reported system-healthy
  interlock : while under hand control the unit runs on the operator's hand start command instead
              of the plant's automatic start command
  interlock : while the unit is under hand intervention, the automatic controlled-shutdown request
              for this unit is suppressed
  interlock : one plant-wide operator reset clears this unit's latched faults
  behaviour : the unit declares itself enabled only after it has been confirmed running for a
              configurable fan start-up (up-to-speed) time; commissioning value 10 s
  behaviour : on a controlled shutdown request the unit stops, and declares its shutdown complete
              once a configurable shutdown time has elapsed and it has stopped running
  behaviour : the unit also declares shutdown complete immediately if it is faulted, or if it is
              under hand control and the operator is not calling it to run
  behaviour : a fail-to-run fault is raised and latched if it is commanded but does not report
              running within a configurable fault time
  behaviour : a fail-to-stop fault is raised and latched if it reports running while not commanded
              for that same fault time
  behaviour : running state is taken from the unit's own running feedback; no motion/rotation sensor
  behaviour : running hours are totalised
  behaviour : operator status indication (faulted / stopped / commanded / running / enabled)
  behaviour : operator alarm indication for fail-to-run, fail-to-stop, unit fault, not-in-remote
  delta (Δ) : this unit's fault condition is derived from the ABSENCE of its system-OK report,
              rather than from a fault report as the class requires. A healthy-report-inverted fault
              differs from a fault report in its failure mode (a dead reporting path reads as
              faulted rather than healthy) — recorded because that difference is a design decision,
              not a notation choice.
  delta (Δ) : this unit reports its running state and its stopped state as two separate reports
              from the field, where the class requires only a running feedback. How the two combine
              (and what a disagreement means) is not settled by the source — Q-A07.
  delta (+) : the plant-wide fault reset is additionally echoed back to the unit itself as a
              reset command to the equipment
  delta (?) : the unit's remote-operational report from the field is brought into the control layer;
              not named in the class requirement set — candidate plus-delta (Q-A08)
  delta (−) : candidate minus-delta — no downstream-ready start permissive (see the interlock line
              above and Q-A05)
  deltas    : none-further-found — searched: source inventory row 19 (all six columns), source
              REQ-005/006/007/013/019/020 text, FilterUnitSystem reference §"Class requirement set" and
              §"Class deltas", source open questions Q-01…Q-08. NOT searched (invisible here by
              construction): IO table / operator-bypass signals — re-hunt at rung C §8.
  provenance: shutdown hold + extra gate <- inventory row 19; "(runs with plant)" <- inventory row
              19 "Starts after" column; fault-from-system-OK and the separate running/stop reports
              <- REQ-019 text; fan start-up time <- REQ-020; class behaviours <- FilterUnitSystem
              reference FU-01…FU-17.
```

## Instance: Discharge Conveyor VSD — `MotorVSDInst1` : vsd-motor

```
Discharge Conveyor VSD (MotorVSDInst1) : vsd-motor
  fed-by                : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  discharges-to         : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  serves (enable-wise)  : Air-Separator VSD (that machine's start permissive names this machine, on
                          both of its alternative start paths)
  interlock : start requires the Overband Magnet to be enabled
  interlock : start requires the Equipment Control System to be enabled
  interlock : controlled shutdown holds until the Air-Separator VSD reports its shutdown complete
  interlock : start requires the plant pre-start warning phase to have completed
  interlock : start requires the plant to be in its running state
  interlock : the motor does not start while it is inhibited (isolated / locked off)
  interlock : the motor does not start while it has an active fault, and does not start while a
              fail-to-run fault is standing
  interlock : the motor does not start unless it is reported system-healthy
  interlock : while under hand control the motor runs on the operator's hand start command instead
              of the plant's automatic start command
  interlock : while this machine is under hand intervention, the automatic controlled-shutdown
              request for it is suppressed
  interlock : one plant-wide operator reset clears this machine's latched faults
  behaviour : the motor declares itself enabled only after it has been confirmed running for a
              configurable up-to-speed time
  behaviour : on a controlled shutdown request the motor stops, and declares its shutdown complete
              once a configurable shutdown time has elapsed and it has stopped running; it also
              declares shutdown complete immediately if it is faulted
  behaviour : the motor runs to a speed setpoint — an automatic setpoint in auto, an operator
              setpoint in hand, floored at a minimum running speed and delivered as a demand scaled
              against the machine's rated maximum speed. **The automatic setpoint's source and value
              are NOT ESTABLISHED for this instance — Q-A10 (blocking).**
  behaviour : run-confirmation supervision is not armed until a configurable start-up allowance has
              elapsed after the drive is commanded and its demand has settled, and is re-armed on a
              direction change, so a ramp or reversal does not itself raise a fault
  behaviour : after that allowance, a fail-to-run fault is raised and latched if commanded without a
              running report within a configurable fault time; a fail-to-stop fault is raised and
              latched if it reports running while not commanded for that same time
  behaviour : a drive error reported by the VSD raises a latched fault
  behaviour : the drive's own condition is brought back for the operator (ready, operation enabled,
              warning, over-speed, under-speed, over-temperature, thermal overload, torque-limit-OK,
              motor current, speed feedback, speed reached)
  behaviour : running hours are totalised
  behaviour : operator status indication (faulted / stopped / commanded / running / enabled)
  behaviour : operator alarm indication for fail-to-run, fail-to-stop, drive error
  delta (Δ) : this instance takes its "running forward" confirmation from a motion/rotation sensor
              on the belt — motion sensed means running — rather than from the drive's own forward
              running report. (The class declares a motion-sensor input but never uses it; any use
              is an instance decision, so this is an instance delta by the class reference's own
              statement.)
  delta (+) : the operator can individually bypass this conveyor's motion-sensor check (failed
              sensor / commissioning). **What confirms "running forward" while the bypass is active
              is NOT ESTABLISHED — Q-A11 (blocking).**
  delta (−) : candidate minus-delta — no reverse operation is specified for this instance, although
              the class provides a reverse direction and reverse running confirmation. The source
              assigns reversal to one other machine only (the shredder feed conveyor). Candidate,
              not settled — Q-A12.
  deltas    : none-further-found — searched: source inventory row 6 (all six columns), source
              REQ-002/005/006/007/008/009/010/011/012 text, vsd-motor reference §"Class requirement
              set" and §"Class deltas against the sibling classes", source open questions Q-01…Q-08.
              NOT searched (invisible here by construction): IO table / operator-bypass signals —
              re-hunt at rung C §8.
  provenance: start permissives + shutdown hold + "rotation-sensor running FB" extra gate <-
              inventory row 6; motion-sensor-as-running-confirmation and its bypass <- REQ-012 and
              REQ-011; class behaviours <- vsd-motor reference VSD-01…VSD-20; hand suppression <-
              REQ-006; plant-wide reset <- REQ-007.
```

## Instance: Sorter Conveyor VSD — `MotorVSDInst3` : vsd-motor

```
Sorter Conveyor VSD (MotorVSDInst3) : vsd-motor
  fed-by                : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  discharges-to         : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  serves (enable-wise)  : Dust Filter Unit 1, Dust Filter Unit 2, Equipment Control System
                          (all three name this machine's enable as their start permissive)
  interlock : start requires the Optical Sorter to report itself ready
  interlock : start requires the Optical Sorter to be free of faults
  interlock : start requires the Ejected Material Conveyor to be enabled
  interlock : start requires the Residual Material Conveyor to be enabled
  interlock : controlled shutdown holds until the Equipment Control System reports its shutdown
              complete
  interlock : start requires the plant pre-start warning phase to have completed
  interlock : start requires the plant to be in its running state
  interlock : the motor does not start while it is inhibited (isolated / locked off)
  interlock : the motor does not start while it has an active fault, and does not start while a
              fail-to-run fault is standing
  interlock : the motor does not start unless it is reported system-healthy
  interlock : while under hand control the motor runs on the operator's hand start command instead
              of the plant's automatic start command
  interlock : while this machine is under hand intervention, the automatic controlled-shutdown
              request for it is suppressed — **but see delta (Δ) below: the source records this
              machine as gating its shutdown on a DIFFERENT machine's hand intervention**
  interlock : one plant-wide operator reset clears this machine's latched faults
  behaviour : the motor declares itself enabled only after it has been confirmed running for a
              configurable up-to-speed time
  behaviour : on a controlled shutdown request the motor stops, and declares its shutdown complete
              once a configurable shutdown time has elapsed and it has stopped running; it also
              declares shutdown complete immediately if it is faulted
  behaviour : the motor runs to a speed setpoint — automatic in auto, operator in hand, floored at a
              minimum running speed, delivered as a demand scaled against rated maximum speed.
              **The automatic setpoint's source and value are NOT ESTABLISHED — Q-A10 (blocking).**
  behaviour : run-confirmation supervision is not armed until a configurable start-up allowance has
              elapsed after the drive is commanded and its demand has settled, re-armed on a
              direction change
  behaviour : after that allowance, fail-to-run and fail-to-stop faults are raised and latched on
              their respective configurable fault times
  behaviour : a drive error reported by the VSD raises a latched fault
  behaviour : the drive's own condition is brought back for the operator (as for the sibling VSD)
  behaviour : running hours are totalised
  behaviour : operator status indication; operator alarm indication for fail-to-run, fail-to-stop,
              drive error
  delta (Δ) : the source records this machine's automatic-shutdown suppression as keyed to the
              **Optical Sorter's** hand-intervention state rather than its own. The source itself
              cannot say whether this is deliberate grouped-hand behaviour or an as-built defect.
              **Retained as an unresolved delta — Q-A13 (blocking).** Not silently normalised to the
              per-machine rule, and not silently adopted either.
  delta (−) : candidate minus-delta — no motion/rotation-sensor use is specified for this conveyor,
              although the class provides the input and the sibling Discharge Conveyor VSD uses one.
              The source's list of motion-confirmed conveyors does not include this machine.
              Candidate, not settled — Q-A14.
  delta (−) : candidate minus-delta — no reverse operation specified for this instance (Q-A12).
  deltas    : none-further-found — searched: source inventory row 10 (all six columns), source
              REQ-002/005/006/007/011/017 text, vsd-motor reference §"Class requirement set" and
              §"Class deltas", source open questions Q-01…Q-08 (Q-07 raised delta Δ above).
              NOT searched (invisible here by construction): IO table / operator-bypass signals —
              re-hunt at rung C §8.
  provenance: start permissives + shutdown hold <- inventory row 10; sorter-ready/not-faulted
              permissive also stated at REQ-017; class behaviours <- vsd-motor reference
              VSD-01…VSD-20; cross-machine hand-intervention delta <- source Q-07; motion-sensor
              absence <- REQ-011 (machine not listed among the motion-confirmed conveyors).
```

## Instance: Optical Sorter — `TomraControlInst1` : optical-sorter

```
Optical Sorter (TomraControlInst1) : optical-sorter
  fed-by                : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  discharges-to         : NOT ESTABLISHED — no material-flow source exists (Q-A03)
  serves (enable-wise)  : Sorter Conveyor VSD (that machine's start permissive names this machine's
                          ready-and-not-faulted state — note this is NOT the normal "enabled"
                          relation used everywhere else in the plant; see Q-A15)
  interlock : start requires the Ejected Material Conveyor to be enabled
  interlock : start requires the Residual Material Conveyor to be enabled
  interlock : controlled shutdown holds until the Sorter Conveyor VSD reports its shutdown complete
  interlock : start requires the plant pre-start warning phase to have completed
  interlock : start requires the plant to be in its running state
  interlock : the sorter does not start while it is inhibited (isolated / locked off)
  interlock : the sorter does not start while it has an active fault, and does not start while a
              fail-to-run fault is standing
  interlock : while under hand control the sorter runs on the operator's hand start command instead
              of the plant's automatic start command
  interlock : while this machine is under hand intervention, the automatic controlled-shutdown
              request for it is suppressed — **but see delta (Δ): the source records this machine as
              gating its shutdown on a DIFFERENT machine's hand intervention**
  interlock : one plant-wide operator reset clears this machine's latched faults, and the reset is
              also passed to the sorter itself over the data link
  behaviour : the sorter is commanded both over a data link and by a hardwired run signal, both
              driven together from the same run decision
  behaviour : the sorter's condition is read both over the data link and from hardwired ready,
              running and communications-fault signals
  behaviour : the data link is supervised by a liveness indication from the sorter; if liveness is
              lost for a fixed watchdog period a communications-lost condition is latched until reset
  behaviour : while the data link is healthy the sorter counts as running only when both the link
              and the hardwired running signal say so; while the link is dead the hardwired running
              signal alone decides — and the same fallback applies to its communications-fault
              indication
  behaviour : once running for a configurable up-to-speed time, and while the sorter reports itself
              ready, the sorter declares itself enabled; it withdraws that enable IMMEDIATELY if it
              stops running (unlike the sibling classes, whose enable falls only with the timer)
  behaviour : on a controlled shutdown request the sorter stops, and declares its shutdown complete
              once a configurable shutdown time has elapsed and it has stopped running
  behaviour : fail-to-run and fail-to-stop faults are raised and latched on their configurable fault
              times
  behaviour : any of the sorter's own reported machine faults, plus loss of the data link, raises a
              latched fault
  behaviour : running hours are totalised
  behaviour : operator status indication; a wide operator alarm indication covering the sorter's own
              reported fault set plus fail-to-run, fail-to-stop and loss of the data link
  behaviour : the sorting program to run and the sorter's belt on/off delays are operator-settable
  behaviour : the byte order of the data exchanged with the sorter is selectable at plant level
  delta (Δ) : the source records this machine's automatic-shutdown suppression as keyed to the
              **Sorter Conveyor VSD's** hand-intervention state rather than its own — the mirror of
              the same unresolved item on that machine. **Q-A13 (blocking).**
  delta (?) : the source records "comms words" as this machine's extra gate and leaves it explicitly
              open whether the process-data word interface is in scope at all, and what the words
              carry. **Q-A16 (blocking) — scope, not detail.**
  class-risk : the class reference for this machine was derived from ONE instance, so nothing
              peculiar to this machine can be distinguished from a class property (Q-A02).
  class-risk : the class reference records three observed as-built anomalies (its A-1, A-2, A-3)
              which it explicitly declines to state as requirements. They are NOT inherited as
              relations here. Two of them bear on process behaviour this rung would otherwise
              specify — hand-start availability, and operator-settable sorting program/belt delays
              being an interface promise rather than an implemented behaviour — so they are carried
              forward as Q-A17 rather than dropped.
  deltas    : none-further-found — searched: source inventory row 11 (all six columns), source
              REQ-002/005/006/007/017 text, optical-sorter reference §"Class requirement set",
              §"Class deltas against the sibling classes" and §"Observed as-built anomalies", source
              open questions Q-01…Q-08 (Q-06 → Q-A16, Q-07 → Q-A13). NOT searched (invisible here by
              construction): IO table / operator-bypass signals — re-hunt at rung C §8.
  provenance: start permissives + shutdown hold + "comms words" extra gate <- inventory row 11;
              sorter integration <- REQ-017; class behaviours <- optical-sorter reference
              OS-01…OS-19; cross-machine hand-intervention delta <- source Q-07; comms-word scope
              <- source Q-06.
```

---

## Cross-instance observations (facts, not verdicts)

Recorded because they are relations *between* the six machines and their named neighbours, and
because each is a place a downstream rung could silently pick a reading.

- **O-1 — Enable direction runs opposite to the intuitive material path in at least three places.**
  The convention says a machine's start permissive is the enable of the machine it is *served by*.
  The Sorter Conveyor VSD's enable permits **two dust filter units** and the Equipment Control
  System; the Discharge Conveyor VSD's enable permits the Air-Separator VSD. Filter/extraction plant
  serving a conveyor (rather than the reverse) is not obviously a material-flow relation at all.
  This is stated as a fact of the source; whether the underlying relation is material flow, air
  path, or something else is Q-A03.
- **O-2 — A faulted Optical Sorter never releases the machines waiting on its shutdown.** The
  optical-sorter class's shutdown-complete has no fault escape term, unlike both sibling classes
  (whose shutdown-complete is declared immediately on fault). The Ejected and Residual Material
  Conveyors hold through shutdown until the Optical Sorter reports shutdown complete. A faulted
  sorter therefore leaves those two machines running through a controlled shutdown indefinitely.
  Both are out of scope for this run but the relation is in scope. Recorded, not resolved — Q-A18.
- **O-3 — No start-permissive cycle among the six and their named neighbours.** Checked: Sorter
  Conveyor VSD ← Optical Sorter ← Ejected/Residual conveyors; Dust Filters ← Sorter Conveyor VSD;
  Discharge Conveyor VSD ← Overband Magnet, Equipment Control System ← Sorter Conveyor VSD;
  Air-Separator VSD ← Discharge Conveyor VSD + both Dust Filters. The shutdown-hold chain runs the
  other way (Discharge Conveyor VSD holds for Air-Separator VSD, which holds for the Incline Feed
  Conveyor) and is likewise acyclic across these machines.
- **O-4 — The Cyclone Filter Unit is the only one of the six with no start permissive and no served
  machine.** It appears in the shutdown chain (holding for the Shredder) but not in the start chain.
  Q-A05 / Q-A06.
- **O-5 — Two of the six carry the same unresolved cross-machine hand-intervention item, mirrored.**
  Sorter Conveyor VSD ↔ Optical Sorter. Resolving one resolves the other; they cannot be resolved
  independently. Q-A13.

## Explicitness decisions taken (FI-33 audit trail)

Every ambiguous separator encountered in the source, and the branch taken:

| # | Source text | Location | Branch taken | Why |
|---|---|---|---|---|
| E-1 | `Fans-shutdown-ready / Air-separator VSD shut down` | inventory row 3 (Dust Filter Unit 1), "Holds through shutdown until" | **SPLIT-AND-RETAIN** — two hold relations, both kept; combination raised as Q-A04 | Both sides are *conditions*, and both entities are established: the plant fans-shutdown-ready condition is named in the source's own global-signal table (REQ-013), and the Air-Separator VSD is inventory row 5. No new signal, device or channel is asserted by keeping both, so the carve-out does not apply and split-and-retain is the default. Dropping either side would have required a `Q` regardless; combining them silently (AND or OR) is a resolution, so the combination is a separate blocking question. |
| E-2 | `Fans-shutdown-ready / Air-separator VSD shut down` | inventory row 4 (Dust Filter Unit 2) | **SPLIT-AND-RETAIN**, as E-1 | Same reasoning, applied identically — the convention is applied without exception. |
| E-3 | `Fans-shutdown-ready / Shredder shut down` | inventory row 19 (Cyclone Filter Unit) | **SPLIT-AND-RETAIN**, as E-1 | Same reasoning; the second entity here is the Shredder (inventory row 18). |
| E-4 | `Overband Magnet + ECS enabled` | inventory row 6 (Discharge Conveyor VSD), "Starts after" | **FULL ENUMERATION** — two separate start-permissive relations | `+` is a conjunction over two established machines; enumerated rather than carried as one compound relation. Compaction is rung D's problem. |
| E-5 | `Optical Sorter ready & not faulted + Ejected & Residual conveyors enabled` | inventory row 10 (Sorter Conveyor VSD) | **FULL ENUMERATION** — four separate start-permissive relations (sorter ready; sorter not faulted; Ejected Material Conveyor enabled; Residual Material Conveyor enabled) | All four entities established (REQ-017 states the sorter pair explicitly; inventory rows 13/14 for the conveyors). Note the first two are *not* the standard "enabled" relation — flagged as Q-A15 rather than normalised into it. |
| E-6 | `Ejected & Residual conveyors enabled` | inventory row 11 (Optical Sorter) | **FULL ENUMERATION** — two separate start-permissive relations | As E-4. |
| E-7 | `Shredder ready (fwd) / general enable (rev)` | inventory row 17 — **out of scope**, referenced only | **NOT RESOLVED, NOT USED** | The Feed Conveyor is not one of the six. Its `/` is recorded here so that a later run of the remaining machines does not meet it fresh. No reading taken. |
| E-8 | `Discharge-Conveyor VSD + both Dust Filters enabled (or Discharge-Conveyor VSD + third-party link-out)` | inventory row 5 (Air-Separator VSD) — **out of scope**, but names two of the six | **RETAINED AS TWO ALTERNATIVE PATHS, no reading taken** | The source states the alternation explicitly (`or`), so this is not an ambiguous separator; it is recorded because it establishes that both Dust Filter Units serve the Air-Separator VSD on one path only. Whether the alternative path is a degraded mode is the source's own unresolved Q-08. |
| E-9 | `faulted / stopped / commanded / running / enabled` | all three class references, status-indication requirement | **NOT SPLIT — read as an enumeration of the values of one indication, not as separate conditions** | This `/` separates the *states of a single status word*, not conditions of a relation. Stated explicitly here because the FI-33 rule is about conditions, and applying split-and-retain to a value enumeration would manufacture five indications where one exists. |
| E-10 | `remote-operational, running, and fault feedbacks` (REQ-019) | source REQ-019 | **FULL ENUMERATION into three separate reports**, and the "remote-operational" one raised as a candidate delta (Q-A08) rather than assumed to be the class's remote-mode permissive | A comma list of three feedbacks; treating "remote-operational" as identical to the class's remote/local permissive would be a resolution by reading. |

## Open questions

Blocking questions **must be answered before rung C binds signals**; a non-blocking question can
ride to presentation. None is resolved here.

### Blocking

- **Q-A01 — [BLOCKING] The class references are derived from as-built code, not from an engineering
  standard.** All three reference files state this in their own headers: they record "what this
  class is observed to require, as built", which is strictly weaker than "how this class of
  equipment is normally controlled". Completeness-by-construction — the property that makes this
  rung's output trustworthy — is therefore only as good as those files. **A standard requirement
  that the as-built FBs happen not to implement is invisible at this rung and stays invisible
  downstream.** Needed: either a real engineering standard for `FilterUnitSystem`, `vsd-motor` and
  `optical-sorter`, or an explicit owner acceptance that class completeness is not assured for this
  plant.
- **Q-A02 — [BLOCKING] `optical-sorter` has exactly one instance, so class and instance cannot be
  separated.** Anything peculiar to `TomraControlInst1` is indistinguishable from a class property.
  Every "class behaviour" listed for the Optical Sorter above may in fact be an instance delta, and
  every delta may in fact be class behaviour. Needed: a second instance, a vendor/class document, or
  an owner ruling that the single-instance class is accepted as-is.
- **Q-A03 — [BLOCKING] No material-flow source exists.** `fed-by` / `discharges-to` are recorded as
  NOT ESTABLISHED for all six instances. The available source is a dependency graph of enables and
  shutdown-completes, not a P&ID or a flow drawing, and the source's own Q-02 states that the
  material-flow reading of that graph is inferred and unconfirmed. Observation O-1 shows at least
  three relations that do not read as material flow at all. Needed: the P&ID or plant layout.
  *Nothing below was inferred from an assumed flow direction — all relations trace to the stated
  dependency columns.*
- **Q-A04 — [BLOCKING] How do the two shutdown-hold conditions combine?** All three filter units
  hold on **both** "the plant reports the filter fans ready to shut down" **and** a named
  neighbour's shutdown-complete (Air-Separator VSD for the two dust units, Shredder for the
  cyclone). The source joins them with `/`. **AND** (hold until both) and **OR** (hold until either)
  give materially different plant behaviour — under OR a single condition releases the unit early,
  under AND a stuck condition holds a fan running through the whole shutdown. Both sides are
  retained above; the combination is not chosen. Needed: which it is, per unit.
- **Q-A05 — [BLOCKING] Does the Cyclone Filter Unit really have no start permissive?** The source
  says only "(runs with plant)". Every other machine in the inventory names a neighbour whose enable
  gates its start. Needed: either the missing permissive, or confirmation that this unit starts on
  the plant run state alone (a minus-delta against its class).
- **Q-A06 — [BLOCKING] What does the Cyclone Filter Unit's enable permit?** Its class makes
  "declares itself enabled, which permits the machine it serves to start" a core requirement, and
  the unit carries a fan start-up time whose stated purpose is exactly that enable. No machine in
  the inventory names it. Needed: the served machine, or confirmation that the enable is unused
  here (which would make the fan start-up time serve no stated purpose — worth knowing before rung
  C binds it).
- **Q-A10 — [BLOCKING] What is the automatic speed setpoint for the two VSD conveyors?** The
  `vsd-motor` class requires an automatic speed setpoint in auto, floored at a minimum running speed
  and scaled against rated maximum speed. The source names no setpoint source, no value, and no
  rated maximum for either the Discharge Conveyor VSD or the Sorter Conveyor VSD. Needed: the
  setpoint source (fixed, recipe, operator, upstream) and value per machine.
- **Q-A11 — [BLOCKING] What confirms the Discharge Conveyor VSD is running while its motion-sensor
  check is bypassed?** The source says the motion sensor *is* this machine's running-forward
  confirmation, and that when bypassed the sensor "is not used". It does not say what takes its
  place. Two readings with different behaviour: (a) fall back to the drive's own forward running
  report; (b) the machine has no running confirmation at all while bypassed. Needed: which.
- **Q-A13 — [BLOCKING] Cross-machine hand intervention on the Sorter Conveyor VSD and the Optical
  Sorter.** The source records each of these two machines as suppressing its own automatic shutdown
  on the *other* machine's hand-intervention state, and explicitly asks whether that is deliberate
  grouped-hand behaviour or an as-built defect. Two of the six in-scope machines are affected, and
  the item cannot be resolved for one without the other. **Not normalised to the per-machine rule
  and not adopted as intended — held open.** Needed: an owner ruling.
- **Q-A16 — [BLOCKING] Is the Optical Sorter's process-data word interface in scope, and what do
  the words carry?** The source lists "comms words" as this machine's extra gate and states plainly
  that whether reconstructing the word interface is in scope is unclear, and that the words read as
  a given comms buffer rather than a derivable requirement. The class requires the sorter's command
  and condition to travel over that link, so this is a **scope** question, not a detail: without it,
  rung C cannot know whether it is specifying a machine with a data link or one with hardwired
  signals plus an opaque buffer. Needed: an in/out-of-scope ruling and, if in scope, the word map.

### Non-blocking

- **Q-A07 — The Cyclone Filter Unit reports running and stopped separately.** The class requires one
  running feedback. What a disagreement between the two reports means (transitional, or a fault) is
  unsettled. Non-blocking: rung C can bind both and defer the disagreement handling.
- **Q-A08 — Availability/remote-operational reports on all three filter units.** Each unit brings an
  operational-availability report in from the field that the class requirement set does not name.
  Candidate plus-delta. Non-blocking: it may simply be the field side of the class's remote-mode
  permissive, which rung C can settle against the IO table.
- **Q-A09 — Are the two Dust Filter Units genuinely identical?** Their relation sets are identical in
  every field. The source offers no distinguishing datum. Non-blocking, but it is exactly the shape
  of thing that hides a difference (different duct, different served machine, different fan size).
- **Q-A12 — Do either of the two VSD conveyors ever run in reverse?** The class provides a reverse
  direction and reverse running confirmation; the source assigns reversal to one other machine only.
  Recorded as a candidate minus-delta on both. Non-blocking.
- **Q-A14 — Does the Sorter Conveyor VSD have a motion/rotation sensor?** The source's list of
  motion-confirmed conveyors does not include it, while its sibling VSD conveyor is on that list.
  Candidate minus-delta. Non-blocking: rung C's IO sweep can settle it (and is required to, since a
  motion sensor invisible at this rung is exactly the delta class this field exists to catch).
- **Q-A15 — The Optical Sorter permits the Sorter Conveyor VSD by "ready and not faulted", not by
  the standard "enabled" relation.** Everywhere else in the plant a start permissive is the served
  machine's *enable*. This one names two different states. Whether the sorter's enable is
  additionally required, or genuinely replaced by ready-and-not-faulted, is unsettled. Non-blocking
  only because both readings are recorded; it becomes blocking if rung C must pick one.
- **Q-A17 — Two `optical-sorter` class anomalies bear on behaviour this rung would otherwise
  specify.** The class reference logs (its A-2) that hand start is only reachable while the machine
  is faulted, and (its A-3) that the operator-settable sorting program and belt delays are an
  interface promise rather than an implemented behaviour. The reference declines to state either as
  a requirement, so neither is inherited above — but both are process behaviours a specification
  would normally settle. Needed eventually: whether the *intended* behaviour is the class
  requirement (hand start available whenever in hand; program/delays settable) or the observed one.
- **Q-A18 — A faulted Optical Sorter never releases the machines held by its shutdown-complete.**
  See observation O-2. The two affected machines are out of this run's scope, so this is
  non-blocking *here*, but it is a genuine plant-behaviour question, not a modelling artefact.

## Coverage statement

- Instances specified: **6 of 6** in scope (`FilterUnitInst2`, `FilterUnitInst3`, `FilterUnitInst1`,
  `MotorVSDInst1`, `MotorVSDInst3`, `TomraControlInst1`).
- Classes involved: **3** (`FilterUnitSystem`, `vsd-motor`, `optical-sorter`) — **all three have a class
  reference**, so the "class with no reference" stop condition was not triggered. Their derivation
  weakness is Q-A01, which is a blocking question, not a stop.
- Class requirements inherited and restated per instance: FilterUnitSystem FU-01…FU-17 (×3 instances),
  vsd-motor VSD-01…VSD-20 (×2), optical-sorter OS-01…OS-19 (×1).
- Deltas recorded: **13** across the six instances (4 plus, 4 changed, 4 minus-candidates, 1 scope).
  No instance carries a bare `deltas : none`.
- Ambiguous separators met: **10**, all logged in the explicitness table with the branch taken.
- Blocking questions: **10** (Q-A01, A02, A03, A04, A05, A06, A10, A11, A13, A16). Non-blocking: **8**.

**Rung status: BLOCKED.** This artifact is complete as a rung-A product — every in-scope instance is
enumerated with its full relation set — but ten blocking questions stand, four of which
(Q-A03 material flow, Q-A04 hold-condition combination, Q-A05 cyclone start permissive, Q-A16 sorter
comms scope) change what the downstream rungs are specifying, not merely how. Rung B may proceed on
the artifact as written; **rung C must not bind a signal to any relation that a blocking question
governs** without a recorded owner answer.
