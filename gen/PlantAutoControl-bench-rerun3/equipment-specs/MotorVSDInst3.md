# EQUIPMENT SPEC — MotorVSDInst3 (Sorter Conveyor VSD)

    class: vsd-motor (reference v0-derived, 2026-08-04)
    fed-by: not-stated-by-source [A, Q-A4]      discharges-to: not-stated-by-source [A, Q-A4]
    serves: FilterUnitInst2, FilterUnitInst3, ECSControlInst1 [A]

Signal family for this instance: `OSC*` (`MotorVSDInst3.ir` declares `DB MotorVSDInst3`). Distinct from
`OSEC*` (ejected conveyor), `OSRC*` (residual conveyor) and `MotorStarterInst5*` — four `OS*` families.

## IO BINDING [io]

| Role | Signal | Status |
|---|---|---|
| isolation feedback | `DiscreteInputs.OSCIsoFB` | EXISTS |
| motion / rotation sensor | `DiscreteInputs.OSCRotSen` | EXISTS |
| operator bypass of the motion check | **NO SIGNAL** — `HMIControlSignals.BypassOSCRotSen` → MEMBER-NOT-FOUND | **Q-C07** |
| running confirmation | **unresolved** — see C11 / **Q-C06** | — |
| drive error | **NO SIGNAL** in the discrete IO — drive-interface carried | Q-C17 |
| start command out | **NO SIGNAL** — no `OSCStart` in `DiscreteOutputs`; commanded through the drive interface [B-29 names this machine] | resolved on evidence |
| plant fault-reset command in | `HMIControlSignals.SystemReset` | EXISTS |
| pre-start-complete condition | `PlantControl.PreStartComplete` | EXISTS |
| plant run state | `PlantControl.Status` | EXISTS |
| optical-sorter readiness (permissive source) | **unresolved** — see P1 / **Q-C12** | — |
| optical-sorter fault (permissive source) | **unresolved** — see P2 / **Q-C12** | — |

`converter tagstatus` evidence: `OSCIsoFB` EXISTS, `OSCRotSen` EXISTS,
`HMIControlSignals.BypassOSCRotSen` → **MEMBER-NOT-FOUND**, `DiscreteOutputs.OSCStart` →
**MEMBER-NOT-FOUND**.

**The two-signal family check.** `candidate-scan --scope DiscreteInputs.OSC` returned exactly **2**
same-typed IO signals facing 28 FB members. `OSCIsoFB` binds to C2 on the same basis as its sibling
instance (REQ-010/B-25 names the two drives explicitly). `OSCRotSen` does **not** get the equivalent
treatment: unlike `MotorVSDInst1`, **no source states what this instance's motion sensor is for** —
REQ-011 lists this machine in neither its positive nor its negative list (rung B, Q-B5). So the
second signal of the pair is bound to nothing here, deliberately.

## CONTROL REQUIREMENTS (complete by construction from the class reference)

- **C1** the motor runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead [ref VSD-01]
- **C2** the motor does not start while it is inhibited (isolated / locked off)
  [ref VSD-02 + B-25 + io `DiscreteInputs.OSCIsoFB`]
- **C3** the motor does not start while it has an active fault, and does not start while a
  fail-to-run fault is standing [ref VSD-03]
- **C4** the motor does not start unless it is reported system-healthy [ref VSD-04]
  → no signal; health supervised outside this layer [B-32]. Delta. Q-C20.
- **C5** the motor does not start until the plant pre-start warning phase has completed
  [ref VSD-05 + io `PlantControl.PreStartComplete`]
- **C6** the motor runs at a speed setpoint — automatic in auto, operator in hand — floored at a
  minimum running speed and delivered as a demand scaled against the rated maximum speed
  [ref VSD-06] → no plant IO; interface-carried. Values at SETTINGS (none stated).
- **C7** the motor can be commanded to run in reverse as well as forward; its running state is
  confirmed from a forward or a reverse running feedback [ref VSD-07]
  → no reverse output and no reverse running input exist in this instance's family, same shape as
  the sibling instance. **Q-C16 (BLOCKING)** covers both.
- **C8** once running and confirmed running for the up-to-speed time, the motor declares itself
  enabled, and that enable is what permits the machines it serves to start [ref VSD-08]
  → this instance's enable is consumed by **three** machines (P7–P9), the most of any in-scope
  machine, which is what makes C11's unresolved running confirmation load-bearing rather than
  cosmetic.
- **C9** on a controlled shutdown request the motor stops, and declares its shutdown complete once
  the shutdown time has elapsed and both running feedbacks have cleared [ref VSD-09]
- **C10** the motor also declares shutdown complete immediately if it is faulted [ref VSD-10]
- **C11** the motor's running confirmation is taken from a running feedback [ref VSD-07/VSD-08]
      CANDIDATES: `DiscreteInputs.OSCRotSen`
                  `MotorVSDInst3.IO.RunningFwdFB`   [iface, named-only]
                  `MotorVSDInst3.IO.RotationSensor` [iface, named-only]
      → **BLOCKING Q-C06** — decision deferred to D1.
      There is **no `OSCRunning` signal of any kind** in `DiscreteInputs`, so this machine's running
      confirmation cannot be bound from the plant IO the way a starter's can. The motion sensor is
      the only physical candidate, but the documentary basis that made the same inference legitimate
      on `MotorVSDInst1` (REQ-012 names that machine) **does not exist for this one**. Binding it by
      analogy would be exactly the reasoning this rung's candidate rule forbids.
- **C12** run-confirmation supervision is not armed until the start-up time has elapsed after the
  drive is commanded and its demand has settled, and is re-armed on a direction change [ref VSD-11]
- **C13** after that allowance, if the motor is commanded and does not report running within the
  fault time, a fail-to-run fault is raised and latched [ref VSD-12]
- **C14** if the motor reports running while not commanded for that same fault time, a fail-to-stop
  fault is raised and latched [ref VSD-13]
- **C15** a drive error reported by the drive raises a latched fault [ref VSD-14]
  → no plant IO; interface-carried. Q-C17.
- **C16** a fault-reset command clears all latched faults, taken from the single plant-wide operator
  reset [ref VSD-15 + B-15 + io `HMIControlSignals.SystemReset`]
  → see the unclaimed `DiscreteOutputs.VSDFaulrReset`, Q-C21.
- **C17** the drive's own condition is brought back for the operator (ready, operation-enabled,
  warning, over/under-speed, over-temperature, thermal overload, torque-limit-OK, current, speed
  feedback, speed-reached) [ref VSD-16] → interface-carried.
- **C18** a motion/rotation sensor input exists on this class. **A physical motion sensor exists for
  this instance (`DiscreteInputs.OSCRotSen`) but no source says whether or how it is used, and
  unlike every other motion-sensored conveyor in the plant it has NO operator bypass**
  [ref VSD-17 + io] → **BLOCKING Q-C07**
- **C19** running hours are totalised for the motor [ref VSD-18]
- **C20** a status indication is published for the operator [ref VSD-19]
- **C21** *(alarm indication)* [ref VSD-20] → **out of scope for this artifact** (alarms separate).
- **C22** the resulting run command reaches the drive through the drive interface rather than a
  discrete start output [B-29 + evidence: no `OSCStart` exists]

## PLANT INTERLOCKS (fully enumerated, one relation per line)

- **P1** start permitted only while the Optical Sorter (`TomraControlInst1`) reports itself ready
  [A rel-18, B-10]
      CANDIDATES: `DiscreteInputs.TomraReady`
                  `TomraControlInst1.HardWireSignals.Ready` [iface, named-only]
                  `TomraControlInst1.Outputs.UPSEnable`     [iface, named-only]
      → **BLOCKING Q-C12** — decision deferred to D1
- **P2** start permitted only while the Optical Sorter is not faulted [A rel-19, B-10]
      CANDIDATES: `DiscreteInputs.TomraComFlt`             (raw hardwired comms-fault input)
                  `TomraControlInst1.Outputs.FaultActive`  [iface, named-only] (the machine's
                                                            aggregated fault)
                  `TomraControlInst1.Outputs.OSComFault`   [iface, named-only] (the comms-fault
                                                            output, link-aware per B-24)
      → **BLOCKING Q-C12** — decision deferred to D1.
      These are **not** the same condition wearing three names: a raw comms-fault input is true only
      for a communications failure, while the aggregated fault output covers the sorter's whole
      reported fault set plus loss of the data link [ref OS-13]. Binding the narrow one where the
      wide one was meant silently deletes most of this permissive. A merge is forbidden here by §5
      (candidate sets spanning a signal and an interface member are never resolved by merge).
- **P3** start permitted only while the Ejected Material Conveyor (`MotorStarterInst6`) is enabled
  [A rel-20, B-08]
- **P4** start permitted only while the Residual Material Conveyor (`MotorStarterInst7`) is enabled
  [A rel-21, B-08]
- **P5** on controlled shutdown, hold until the Equipment Control System (`ECSControlInst1`) reports
  its shutdown complete [A rel-22, B-05]
- **P6** the Optical Sorter holds its controlled shutdown until this machine reports its shutdown
  complete [A rel-24]
- **P7** this machine's enable permits `FilterUnitInst2` to start [A rel-23]
- **P8** this machine's enable permits `FilterUnitInst3` to start [A rel-23]
- **P9** this machine's enable permits `ECSControlInst1` to start [A rel-23]
- **P10** while the plant is in its running state, this machine is commanded to start and run
  automatically, subject to P1–P5 and C1–C8 [B-02 + io `PlantControl.Status`]
- **P11** **DISPUTED** — while this machine is under hand intervention its automatic
  controlled-shutdown request is suppressed [B-13] **vs** the suppression is gated on **another
  machine's** hand-intervention state [source Q-07, rung B Q-B3]. **BLOCKING Q-C10.** Both readings
  recorded; neither chosen.
- **P12** the start permissives P3 and P4 have **no reciprocal shutdown hold**: those two conveyors
  release on the Optical Sorter's shutdown-complete, not on this machine's [A asymmetry delta].
  Recorded as a relation of the plant, not as a defect claim.

## SETTINGS

| Setting | Owner | Value | Basis |
|---|---|---|---|
| up-to-speed (enable) time | engineering | **not stated** | Q-C15 |
| shutdown-complete time | engineering | **not stated** | Q-C15 |
| fail-to-run / fail-to-stop time | engineering | **not stated** | Q-C15 |
| drive start-up (ramp) allowance | engineering | **not stated by the plant sources** | not invented |
| automatic / hand speed setpoints, minimum speed, rated maximum speed | HMI / engineering | **not stated** | Q-C15 |
| motion-check bypass | — | **does not exist for this machine** | MEMBER-NOT-FOUND, Q-C07 |

## UNCLAIMED IO (§8(a) per-instance sweep)

| Signal | Disposition |
|---|---|
| `DiscreteInputs.OSCIsoFB` | bound — C2 |
| `DiscreteInputs.OSCRotSen` | **UNCLAIMED — BLOCKING Q-C07.** A physical motion sensor exists for this machine and no requirement claims it: no source states that it confirms running (Q-C06), and no operator bypass exists for it. An unclaimed sensor input is exactly the shape of the documented miss this sweep exists to catch. |
| `DiscreteOutputs.VSDFaulrReset` | **UNCLAIMED at instance level — plant-level Q-C21** |
| `DiscreteInputs.TomraReady` / `TomraComFlt` | in the P1/P2 candidate sets — **unresolved, Q-C12** (also bound in `TomraControlInst1.md`; a signal legitimately in two instances' scope) |

Family completeness: `DiscreteInputs.OSC*` is exactly {`IsoFB`, `RotSen`}; `DiscreteOutputs.*OSC*` is
empty; `HMIControlSignals.*OSC*` is empty (which is itself Q-C07's finding).

## DELTAS

- **CONVENTION-DEVIATION** — its permissive on the Optical Sorter is that machine's *ready and
  not-faulted* state, not an up-to-speed enable; the only such permissive among the six in-scope
  machines [A, B-10, Q-A6].
- **ASYMMETRY** — P3/P4 have no reciprocal shutdown hold [A] → P12.
- **MINUS (new at this rung)** — **has a motion sensor but no operator bypass for it**, while all
  seven other motion-sensored conveyors in the plant have one. Established mechanically:
  `HMIControlSignals` holds `BypassHFLCRotSen`, `BypassFMCCRotSen`, `BypassOSECRotSen`,
  `BypassOSRCRotSen`, `BypassSFCRotSen`, `BypassSDCRotSen`, `BypassIFCRotSen`,
  `BypassAirStarDCRotSen` — and no `BypassOSCRotSen`. → Q-C07.
- **MINUS (new at this rung)** — **no running-feedback signal of any kind** exists for this machine
  → Q-C06.
- **MINUS (new at this rung)** — no reverse output and no reverse running input → Q-C16.
- **NONE-FURTHER-FOUND — searched:** rung-A deltas; class reference §Class requirement set, §Class
  deltas and the VSD-17 note; §8(a) sweep above; §8(b) plant residual sweep; `candidate-scan` on
  `DiscreteInputs.OSC` (2 IO) and on the whole of `DiscreteOutputs` (28 IO); `tagstatus` on every
  bound and attempted name, including the three deliberately-probed absences.

## OPEN

- **Q-C06 (BLOCKING)** — this machine's running confirmation has no bindable source
- **Q-C07 (BLOCKING)** — motion sensor exists, unclaimed, and has no operator bypass
- **Q-C10 (BLOCKING)** — whose hand-intervention state suppresses this machine's auto shutdown?
  (carries Q-B3)
- **Q-C12 (BLOCKING)** — which readiness signal and which fault signal satisfy P1/P2?
- **Q-C16 (BLOCKING)** — reverse capability absent or unsourced?
- **Q-C21 (BLOCKING, plant-level)** — `DiscreteOutputs.VSDFaulrReset` claimed by no instance
- **Q-C15 (non-blocking)** — settings with no stated value
- **Q-C17 (non-blocking)** — drive error / drive condition set are interface-carried
- **Q-C20 (BLOCKING, plant-level)** — plant-health residual signals
