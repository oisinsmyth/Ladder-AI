# EQUIPMENT SPEC — Discharge Conveyor VSD (`MotorVSDInst1`)

Rung C (`/gen-equipment-spec`), run `PlantAutoControl-bench-rerun2`, 2026-08-05.
**Signals are bound here. Booleans, interface members and network structure are NOT.**

```
class: vsd-motor (ref v0-derived 2026-08-04)   FB type: MotorVSDSystem
fed-by:        NOT ESTABLISHED — no material-flow source (Q-A03)
discharges-to: NOT ESTABLISHED — no material-flow source (Q-A03)
serves:        Air-Separator VSD (enable-wise, on both of that machine's start paths) [A]
```

**Instance identification:** the `AirStarDC*` signal prefix is bound to this instance on a
documentary basis — the given instance DB for `MotorVSDInst1` is the file `MotorVSDInst1.ir` and
carries the equipment name *"Discharge Conveyor VSD Unit"*, and the source register names
`DiscreteInputs.AirStarDCRotSen` as the discharge-conveyor VSD's rotation sensor (REQ-012).

**Tag verification method:** grep-verified member-by-member; `converter tagstatus`'s pass not relied
on (root-only checking).

## IO BINDING [io]

| Role | Signal | Verified |
|---|---|---|
| motion / rotation sensor | `DiscreteInputs.AirStarDCRotSen` | grep ✓ |
| motion-sensor bypass (operator) | `HMIControlSignals.BypassAirStarDCRotSen` | grep ✓ |
| isolator feedback | `DiscreteInputs.AirStarDCIsoFB` | grep ✓ |
| plant run state | `PlantControl.Status` | grep ✓ |
| plant pre-start complete | `PlantControl.PreStartComplete` | grep ✓ |
| plant-wide operator reset | `HMIControlSignals.SystemReset` | grep ✓ |
| run command → field | **NO DISCRETE OUTPUT EXISTS** — see P10 | — |
| forward running feedback | **NO SIGNAL EXISTS** other than the motion sensor — Q-C08 | — |
| reverse running feedback | **NO SIGNAL EXISTS** — Q-C29 | — |
| drive error / VSD condition | **NO SIGNALS EXIST** in the exported IO — Q-C30 | — |
| speed setpoint / speed feedback | **NO SIGNALS EXIST** in the exported IO — Q-C14 | — |
| hand/auto mode source | **NO SIGNAL EXISTS** — Q-C04 | — |
| system-healthy source | **UNRESOLVED** — Q-C05 | — |

## CONTROL REQUIREMENTS (complete by construction from the class reference, VSD-01…VSD-20)

- **C1** the motor runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead `[ref VSD-01]`
  **CANDIDATES:** `InterlockData.PartInHand` (plant-level, wrong granularity) ·
  `HMIControlSignals.GlobalSetAllToAuto` (plant-wide mode command) · `MotorVSDInst1.IO.InHand`
  `[iface, named-only]` → **BLOCKING Q-C04**
- **C2** the motor does not start while it is inhibited (isolated / locked off)
  `[ref VSD-02 + io]` — `DiscreteInputs.AirStarDCIsoFB`
- **C3** the motor does not start while it has an active fault, and does not start while a
  fail-to-run fault is standing `[ref VSD-03]`
- **C4** the motor does not start unless it is reported system-healthy `[ref VSD-04]`
  **CANDIDATES:** `DiscreteInputs.ControlHealthy` · *a hard-asserted constant true per `[B-34]`*
  → **BLOCKING Q-C05**
- **C5** the motor does not start until the plant pre-start warning phase has completed
  `[ref VSD-05 + io]` — `PlantControl.PreStartComplete`
- **C6** the motor runs at a speed setpoint: an automatic setpoint in auto, an operator setpoint in
  hand, floored at a minimum running speed and delivered to the drive as a speed demand scaled
  against the machine's rated maximum speed `[ref VSD-06]`
  **CANDIDATES:** *(no per-machine speed signal exists in the exported IO)*
  `ProcessTimings.GlobalVSDAutoSpeed` + `HMIControlSignals.GlobalVSDAutoSpeedOverwrite` — a
  plant-wide override pair, currently 0.0, claimed by no requirement in any source ·
  `ProcessTimings.GlobalVSDHandSpeed` + `HMIControlSignals.GlobalVSDHandSpeedOverwrite` — likewise ·
  `MotorVSDInst1.IO.AutoSpeedInput` `[iface, named-only]` (given start value 100.0) ·
  `MotorVSDInst1.IO.HandSpeedInput` `[iface, named-only]` (given start value 30.0)
  → **BLOCKING Q-C14** — four candidates in two competing mechanisms (a per-instance setpoint and a
  plant-wide override), and no source states which governs. *The whole speed behaviour of a
  variable-speed drive rests on this; a wrong choice runs a discharge conveyor at the wrong speed
  silently.*
- **C7** the motor can be commanded to run in reverse as well as forward; its running state is
  confirmed from a forward running feedback or a reverse running feedback `[ref VSD-07]`
  **CANDIDATES:** *(empty — the exported IO has neither an `AirStarDCRunning` nor an
  `AirStarDCRunningRev`; the only motion-related signal for this machine is the rotation sensor)*
  → **BLOCKING Q-C29** — the class's reverse capability has no field signal on this instance;
  rung A recorded reverse as a candidate minus-delta (Q-A12) and the IO sweep now confirms **no
  reverse feedback exists**. Recorded as evidence toward that delta, not as its resolution.
- **C8** once running and confirmed running for a configurable up-to-speed time, the motor declares
  itself enabled, which permits the machine it serves to start `[ref VSD-08]`
- **C9** on a controlled shutdown request the motor stops, and declares its shutdown complete once a
  configurable shutdown time has elapsed and both running feedbacks have cleared `[ref VSD-09]`
- **C10** the motor also declares shutdown complete immediately if it is faulted `[ref VSD-10]`
- **C11** run-confirmation supervision is not armed until a configurable start-up time has elapsed
  after the drive is commanded and its speed demand has settled, and it is re-armed whenever the
  direction changes, so a ramp or a direction reversal does not itself raise a fault
  `[ref VSD-11 + B-28]`
- **C12** after that start-up allowance, if the motor is commanded but does not report running
  within a configurable fault time, a fail-to-run fault is raised and latched `[ref VSD-12]`
- **C13** if the motor reports running while not commanded for that same fault time, a fail-to-stop
  fault is raised and latched `[ref VSD-13]`
- **C14** a drive error reported by the VSD raises a latched fault `[ref VSD-14]`
  **CANDIDATES:** *(empty — no drive-error signal for this machine exists in the exported IO)*
  → **BLOCKING Q-C30** — the drive's error report reaches the controller by a path the exported IO
  does not contain (a fieldbus/telegram not in this export is the obvious hypothesis, but it is a
  hypothesis and the signal is **proposed** until an owner confirms it).
- **C15** a fault-reset command clears all latched faults `[ref VSD-15 + io]` —
  `HMIControlSignals.SystemReset`
  *Note: `DiscreteOutputs.VSDFaulrReset` exists, is claimed by no requirement in any source, and is
  named as a VSD fault reset — a second, physical reset path for this machine class.* **Q-C17.**
- **C16** the drive's own condition is brought back for the operator: ready, operation-enabled,
  warning, over-speed, under-speed, over-temperature, thermal overload, torque-limit-OK, motor
  current, speed feedback and speed-reached `[ref VSD-16]`
  **CANDIDATES:** *(empty in the exported IO)* → **BLOCKING Q-C30** (same missing path as C14).
- **C17** a motion/rotation sensor input exists on this class; whether an instance uses it, and for
  what, is an instance-level decision `[ref VSD-17 + io]` — **this instance uses it**:
  `DiscreteInputs.AirStarDCRotSen` is the source of its running-forward confirmation (motion sensed
  = running) `[B-38 + Δ1]`, and the check is individually bypassable by the operator via
  `HMIControlSignals.BypassAirStarDCRotSen` `[B-37 + Δ2]`.
  **What confirms running while the bypass is active is UNRESOLVED — BLOCKING Q-C08.**
  **CANDIDATES for the bypassed-state running confirmation:** *(empty — the IO sweep found no other
  running feedback for this machine, so the "fall back to the drive's own forward running report"
  reading of `[B-38]` has no signal behind it in this export)*
- **C18** running hours are totalised for the motor `[ref VSD-18]`
- **C19** a status indication is published for the operator (faulted / stopped / commanded /
  running / enabled) `[ref VSD-19]`
- **C20** an alarm indication is published for fail-to-run, fail-to-stop, and drive error
  `[ref VSD-20]` — *alarms out of scope for this control spec; recorded for completeness.*

## PLANT INTERLOCKS

- **P1** start permitted only while the Overband Magnet (`MotorStarterInst8`) is enabled `[A + B-08]`
- **P2** start permitted only while the Equipment Control System (`ECSControlInst1`) is enabled
  `[A + B-08]`
- **P3** on controlled shutdown, hold until the Air-Separator VSD (`AirStarInst1`) reports its
  shutdown complete `[A + B-12]`
- **P4** start permitted only while the plant is in its running state `[A + B-02 + io]` —
  `PlantControl.Status`
- **P5** start permitted only after the plant pre-start warning phase has completed
  `[A + B-05 + io]` — `PlantControl.PreStartComplete`
- **P6** while this machine is under hand intervention, its automatic controlled-shutdown request is
  suppressed `[A + B-19]`
- **P7** this machine's latched faults are cleared by the single plant-wide operator reset
  `[A + B-24 + io]` — `HMIControlSignals.SystemReset`
- **P8** this machine's enable is a start permissive for the Air-Separator VSD, on **both** of that
  machine's alternative start paths `[A + B-44]`
- **P9** the motor is inhibited from running while its local isolator feedback indicates it is
  isolated `[A + B-39 + io]` — `DiscreteInputs.AirStarDCIsoFB` *(same fact as C2; merged, both
  cited)*
- **P10** the machine's resulting run command is delivered through its drive interface, **not**
  through a discrete start output `[B-40 + io-absence]` — the exported `DiscreteOutputs` contains no
  start bit for this machine (`HFLCStart`, `AirStarFCStart`, … exist; no `AirStarDCStart`).
  *This is recorded as an IO-evidenced finding, not an assumption: rung B left the machine's command
  path open (Q-B15) because the source's two lists named it in neither; the absence of any candidate
  output settles which list it belongs to.*

## SETTINGS

| Setting | Owner | Value | Provenance |
|---|---|---|---|
| up-to-speed (enable) time | engineering | **2.0 s** | given instance-DB start value `(boundary)` — not in any plant document `[Q-B21]` |
| shutdown time | engineering | **8.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| fail-to-run / fail-to-stop fault time | engineering | **3.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| start-up (ramp) allowance before supervision arms | engineering | **8.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| rated maximum speed | engineering | **1500** | given instance-DB start value `(boundary)` `[Q-B17]` |
| automatic speed setpoint | HMI / engineering — **owner unresolved** | **100.0** per instance DB, **0.0** per the plant-wide override | **Q-C14** — two mechanisms, two values, no source |
| operator (hand) speed setpoint | HMI | **30.0** per instance DB, **0.0** per the plant-wide override | **Q-C14** |
| minimum running speed | engineering | **not stated in any source available to this rung** | **Q-B17** — the class requires a floor; no value exists |
| running-hours totaliser interval | engineering | not initialised in the given boundary | `(boundary)` |

## UNCLAIMED IO (§8a per-instance sweep)

| Signal | Disposition |
|---|---|
| `DiscreteInputs.AirStarDCRotSen` | bound — C17 |
| `DiscreteInputs.AirStarDCIsoFB` | bound — C2, P9 |
| `HMIControlSignals.BypassAirStarDCRotSen` | bound — C17 |
| *(no `AirStarDCRunning` / `…RunningRev`)* | absent — Q-C08, Q-C29 |
| *(no `AirStarDCStart`)* | absent by design — P10 |
| `DiscreteOutputs.VSDFaulrReset` | **UNCLAIMED, control-implying** — a VSD fault-reset output with no owner; this machine and `MotorVSDInst3` are both candidates, and no source mentions it. **BLOCKING Q-C17.** |
| `ProcessTimings.GlobalVSDAutoSpeed` / `GlobalVSDHandSpeed` / `GlobalVSDStartUpTime` and their `HMIControlSignals.Global*Overwrite` flags | **UNCLAIMED, control-implying** — plant-wide overrides of this machine's speed and ramp settings. **BLOCKING Q-C15 / Q-C14.** |
| `HMIControlSignals.GlobalSetAllToAuto` | UNCLAIMED, control-implying — **Q-C25** |
| `DiscreteInputs.SurgeProtection`, `DiscreteInputs.ControlHealthy` | UNCLAIMED plant-level, control-implying — **Q-C05 / Q-C31** |

## DELTAS

- **Δ1 (Δ) on C17** — running-forward confirmation is taken from the belt motion sensor, not from a
  drive running report. The class declares the sensor input but never uses it, so any use is an
  instance decision. `[A + B-38]`
- **Δ2 (+) on C17** — the motion-sensor check is individually bypassable by the operator.
  `[A + B-37 + io]`
- **Δ3 (−) on C7** — no reverse operation and, per the §8a sweep, **no reverse feedback signal**.
  Q-C29 / Q-A12.
- **Δ4 (−) on C14/C16** — the drive's error and condition reports have no signals in the exported IO.
  Q-C30.
- **Δ5 (Δ) on P10** — commanded through the drive interface rather than a discrete start output,
  evidenced by the absence of any candidate output.

**DELTAS: none-further-found** — searched: rung-A delta block for this instance, `vsd-motor`
reference §"Class requirement set" and §"Class deltas", rung-B behaviours scoped to this instance
(B-02, 05, 08, 09, 12, 14, 15, 19, 20, 24, 27, 28, 29, 34, 35, 36, 37, 38, 39, 40, 44, 45, 46, 50,
51, 52), the §8a sweep above, the §8b plant residual list, and a field comparison against
`MotorVSDInst3`.

## OPEN

**Blocking:** Q-C04, Q-C05, Q-C08, Q-C14, Q-C15, Q-C17, Q-C25, Q-C29, Q-C30, Q-C31.
**Carried, still blocking:** Q-A01, Q-A03, Q-A10, Q-A11, Q-B01, Q-B12, Q-B13, Q-B17.
**Non-blocking:** Q-A12, Q-B15 (answered by IO absence at P10, recorded), Q-B21.
