# EQUIPMENT SPEC — Sorter Conveyor VSD (`MotorVSDInst3`)

Rung C (`/gen-equipment-spec`), run `PlantAutoControl-bench-rerun2`, 2026-08-05.
**Signals are bound here. Booleans, interface members and network structure are NOT.**

```
class: vsd-motor (ref v0-derived 2026-08-04)   FB type: MotorVSDSystem
fed-by:        NOT ESTABLISHED — no material-flow source (Q-A03)
discharges-to: NOT ESTABLISHED — no material-flow source (Q-A03)
serves:        Dust Filter Unit 1, Dust Filter Unit 2, Equipment Control System (enable-wise) [A]
```

**Instance identification:** the `OSC*` signal prefix is bound to this instance on a documentary
basis — the given instance DB for `MotorVSDInst3` is the file `MotorVSDInst3.ir` and carries the
equipment name *"Sorter Conveyor VSD Unit"*. No source register text names an `OSC*` signal, so this
binding rests on the instance-DB naming alone; recorded as **Q-C32** (non-blocking) so an owner can
confirm it rather than have it pass unnoticed.

**Tag verification method:** grep-verified member-by-member; `converter tagstatus`'s pass not relied
on — and in this instance's case actively wrong: it reports `HMIControlSignals.BypassOSCRotSen ->
EXISTS` when that member does **not** exist in the DB (see Q-C09).

## IO BINDING [io]

| Role | Signal | Verified |
|---|---|---|
| isolator feedback | `DiscreteInputs.OSCIsoFB` | grep ✓ |
| motion / rotation sensor | `DiscreteInputs.OSCRotSen` | grep ✓ — **exists, and no source requirement claims it (Q-C09)** |
| motion-sensor bypass (operator) | **`HMIControlSignals.BypassOSCRotSen` DOES NOT EXIST** — proposed, Q-C09 | grep ✗ (0 hits) |
| sorter ready (start permissive) | `DiscreteInputs.TomraReady` | grep ✓ |
| sorter comms fault (start permissive) | `DiscreteInputs.TomraComFlt` | grep ✓ |
| plant run state | `PlantControl.Status` | grep ✓ |
| plant pre-start complete | `PlantControl.PreStartComplete` | grep ✓ |
| plant-wide operator reset | `HMIControlSignals.SystemReset` | grep ✓ |
| run command → field | **NO DISCRETE OUTPUT EXISTS** — drive interface, see P13 | — |
| forward / reverse running feedback | **NO SIGNALS EXIST** — Q-C33 | — |
| drive error / VSD condition | **NO SIGNALS EXIST** in the exported IO — Q-C30 | — |
| speed setpoint / feedback | **NO SIGNALS EXIST** in the exported IO — Q-C14 | — |
| hand/auto mode source | **NO SIGNAL EXISTS** — Q-C04 | — |
| system-healthy source | **UNRESOLVED** — Q-C05 | — |

## CONTROL REQUIREMENTS (complete by construction, VSD-01…VSD-20)

- **C1** the motor runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead `[ref VSD-01]`
  **CANDIDATES:** `InterlockData.PartInHand` · `HMIControlSignals.GlobalSetAllToAuto` ·
  `MotorVSDInst3.IO.InHand` `[iface, named-only]` → **BLOCKING Q-C04**
- **C2** the motor does not start while it is inhibited (isolated / locked off) `[ref VSD-02 + io]`
  — `DiscreteInputs.OSCIsoFB`
- **C3** the motor does not start while it has an active fault, and does not start while a
  fail-to-run fault is standing `[ref VSD-03]`
- **C4** the motor does not start unless it is reported system-healthy `[ref VSD-04]`
  **CANDIDATES:** `DiscreteInputs.ControlHealthy` · *a hard-asserted constant true per `[B-34]`*
  → **BLOCKING Q-C05**
- **C5** the motor does not start until the plant pre-start warning phase has completed
  `[ref VSD-05 + io]` — `PlantControl.PreStartComplete`
- **C6** the motor runs at a speed setpoint: automatic in auto, operator in hand, floored at a
  minimum running speed, delivered as a demand scaled against rated maximum speed `[ref VSD-06]`
  **CANDIDATES:** `ProcessTimings.GlobalVSDAutoSpeed` + `HMIControlSignals.GlobalVSDAutoSpeedOverwrite`
  · `ProcessTimings.GlobalVSDHandSpeed` + `HMIControlSignals.GlobalVSDHandSpeedOverwrite` ·
  `MotorVSDInst3.IO.AutoSpeedInput` `[iface, named-only]` (given start value 100.0) ·
  `MotorVSDInst3.IO.HandSpeedInput` `[iface, named-only]` (given start value 20.0)
  → **BLOCKING Q-C14** — same two competing mechanisms as the sibling VSD, and note the hand
  setpoints differ between the two machines (20.0 here, 30.0 there) with no source for either.
- **C7** the motor can be commanded to run in reverse as well as forward; running is confirmed from
  a forward or a reverse running feedback `[ref VSD-07]`
  **CANDIDATES:** *(empty — no `OSCRunning`, no `OSCRunningRev` in the exported IO)*
  → **BLOCKING Q-C33** — this machine has **no running feedback signal of any kind** except the
  unclaimed motion sensor of Q-C09. Rung B scoped B-35 ("running confirmed from field running
  feedback") to all six instances; for this one the signal does not exist.
- **C8** once running and confirmed running for a configurable up-to-speed time, the motor declares
  itself enabled, which permits the machines it serves to start `[ref VSD-08]`
- **C9** on a controlled shutdown request the motor stops, and declares its shutdown complete once a
  configurable shutdown time has elapsed and both running feedbacks have cleared `[ref VSD-09]`
- **C10** the motor also declares shutdown complete immediately if it is faulted `[ref VSD-10]`
- **C11** run-confirmation supervision is not armed until a configurable start-up time has elapsed
  after the drive is commanded and its demand has settled, re-armed on a direction change
  `[ref VSD-11 + B-28]`
- **C12** after that allowance, a fail-to-run fault is raised and latched if commanded without a
  running report within a configurable fault time `[ref VSD-12]`
- **C13** a fail-to-stop fault is raised and latched if the motor reports running while not
  commanded for that same fault time `[ref VSD-13]`
- **C14** a drive error reported by the VSD raises a latched fault `[ref VSD-14]`
  **CANDIDATES:** *(empty)* → **BLOCKING Q-C30**
- **C15** a fault-reset command clears all latched faults `[ref VSD-15 + io]` —
  `HMIControlSignals.SystemReset`; `DiscreteOutputs.VSDFaulrReset` is an unclaimed second candidate
  path — **Q-C17**
- **C16** the drive's own condition is brought back for the operator (ready, operation-enabled,
  warning, over/under-speed, over-temperature, thermal overload, torque-limit-OK, current, speed
  feedback, speed-reached) `[ref VSD-16]`
  **CANDIDATES:** *(empty in the exported IO)* → **BLOCKING Q-C30**
- **C17** a motion/rotation sensor input exists on this class; whether an instance uses it, and for
  what, is an instance-level decision `[ref VSD-17]`
  **The IO table contains `DiscreteInputs.OSCRotSen` — a motion sensor for this conveyor — and NO
  source requirement uses it.** Rung A recorded "no motion sensor for this machine" as a candidate
  minus-delta (Q-A14) because the source's list of motion-confirmed conveyors omits it; the §8 sweep
  now shows the sensor physically exists. **BLOCKING Q-C09.** *This is precisely the delta class the
  §8 reverse sweep exists to catch: a signal the specification never mentions, wired to a machine
  whose specification says it has no such input.*
  **And the operator bypass that `[B-37]` requires for every motion-confirmed conveyor does not
  exist for this one** — `HMIControlSignals.BypassOSCRotSen` is **proposed**, while the other eight
  conveyors each have theirs. Never invented (hard rule 3).
- **C18** running hours are totalised `[ref VSD-18]`
- **C19** a status indication is published for the operator `[ref VSD-19]`
- **C20** an alarm indication is published for fail-to-run, fail-to-stop, drive error
  `[ref VSD-20]` — *alarms out of scope for this control spec.*

## PLANT INTERLOCKS

- **P1** start permitted only while the Optical Sorter (`TomraControlInst1`) reports itself ready
  `[A + B-11 + io]` — `DiscreteInputs.TomraReady`
- **P2** start permitted only while the Optical Sorter is free of faults `[A + B-11 + io]` —
  `DiscreteInputs.TomraComFlt`
  **CANDIDATES:** `DiscreteInputs.TomraComFlt` (the raw hardwired communications-fault input) ·
  `TomraControlInst1.Outputs.FaultActive` `[iface, named-only]` (the sorter FB's aggregated fault,
  which per the class reference covers the sorter's whole reported fault set **plus** loss of the
  data link) · `TomraControlInst1.Outputs.OSComFault` `[iface, named-only]` (the FB's
  link-aware comms-fault output, which falls back to the hardwired signal when the link is dead)
  → **BLOCKING Q-C34.** *"Not faulted" satisfied by both a raw comms-fault input and an aggregated
  FB fault output is a documented mis-binding: the raw input covers only the comms path, while the
  sorter's own machine faults would leave this permissive satisfied. The two readings differ in
  exactly the case the interlock exists for.*
  > **P1 and P2 are two lines, never one.** Both must hold, and they resolve to different signals.
  > Rung A recorded this permissive as the plant's only "ready and not faulted" pair rather than the
  > standard "enabled" relation (Q-A15) — see P3.
- **P3** whether this machine ALSO requires the Optical Sorter's *enable* (the standard relation
  everywhere else in the plant) is **NOT ESTABLISHED** `[A Q-A15]` → **BLOCKING Q-C35.**
  *Recorded as its own relation line with an unresolved right-hand side rather than folded into
  P1/P2: if the enable is additionally required, omitting it drops an interlock; if it is not, adding
  it invents one.*
- **P4** start permitted only while the Ejected Material Conveyor (`MotorStarterInst6`) is enabled
  `[A + B-08]`
- **P5** start permitted only while the Residual Material Conveyor (`MotorStarterInst7`) is enabled
  `[A + B-08]`
- **P6** on controlled shutdown, hold until the Equipment Control System (`ECSControlInst1`) reports
  its shutdown complete `[A + B-12]`
- **P7** start permitted only while the plant is in its running state `[A + B-02 + io]` —
  `PlantControl.Status`
- **P8** start permitted only after the plant pre-start warning phase has completed
  `[A + B-05 + io]` — `PlantControl.PreStartComplete`
- **P9** the automatic controlled-shutdown request for this machine is suppressed while **hand
  intervention is active** — but *whose* hand intervention is **NOT ESTABLISHED**: the source states
  the per-machine rule `[B-19]` and separately records this machine keying off the **Optical
  Sorter's** hand state `[A delta + B-21]`. → **BLOCKING Q-C13.** Neither reading adopted.
- **P10** this machine's latched faults are cleared by the single plant-wide operator reset
  `[A + B-24 + io]` — `HMIControlSignals.SystemReset`
- **P11** this machine's enable is the start permissive for Dust Filter Unit 1 (`FilterUnitInst2`)
  and Dust Filter Unit 2 (`FilterUnitInst3`) `[A + B-08]`
- **P12** this machine's enable is the start permissive for the Equipment Control System
  (`ECSControlInst1`) `[A + B-08]`
- **P13** the machine's resulting run command is delivered through its drive interface, not a
  discrete start output `[B-40 + io-absence]` — the source names the sorter-conveyor VSD explicitly
  among the FB/drive-commanded machines (REQ-008 notes), and no `OSCStart` exists in
  `DiscreteOutputs`.
- **P14** the motor is inhibited from running while its local isolator feedback indicates it is
  isolated `[A + B-39 + io]` — `DiscreteInputs.OSCIsoFB` *(same fact as C2; merged, both cited)*

## SETTINGS

| Setting | Owner | Value | Provenance |
|---|---|---|---|
| up-to-speed (enable) time | engineering | **2.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| shutdown time | engineering | **5.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| fail-to-run / fail-to-stop fault time | engineering | **5.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| start-up (ramp) allowance | engineering | **5.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| rated maximum speed | engineering | **1500** | given instance-DB start value `(boundary)` |
| automatic speed setpoint | **owner unresolved** | **100.0** per instance DB, **0.0** per plant-wide override | **Q-C14** |
| operator (hand) speed setpoint | HMI | **20.0** per instance DB (sibling VSD: 30.0), **0.0** per override | **Q-C14** |
| minimum running speed | engineering | **not stated in any source available to this rung** | **Q-B17** |

## UNCLAIMED IO (§8a per-instance sweep)

| Signal | Disposition |
|---|---|
| `DiscreteInputs.OSCIsoFB` | bound — C2, P14 |
| `DiscreteInputs.OSCRotSen` | **UNCLAIMED — no requirement in any source uses it. BLOCKING Q-C09.** |
| *(no `HMIControlSignals.BypassOSCRotSen`)* | **proposed — required by `[B-37]` if the sensor is used. BLOCKING Q-C09.** |
| *(no `OSCRunning` / `OSCRunningRev`)* | absent — Q-C33 |
| *(no `OSCStart`)* | absent by design — P13 |
| `DiscreteInputs.TomraReady`, `TomraComFlt` | bound — P1, P2 (P2 unresolved, Q-C34) |
| `DiscreteOutputs.VSDFaulrReset` | UNCLAIMED, control-implying — **Q-C17** |
| global VSD speed/ramp override set | UNCLAIMED, control-implying — **Q-C14 / Q-C15** |
| `HMIControlSignals.GlobalSetAllToAuto` | UNCLAIMED, control-implying — **Q-C25** |

## DELTAS

- **Δ1 (Δ) on P9** — the source records this machine's shutdown suppression as keyed to the Optical
  Sorter's hand state rather than its own. Held open (Q-C13), mirrored on `TomraControlInst1`.
- **Δ2 (?) on C17** — a motion sensor exists for this conveyor that no requirement claims, and the
  per-conveyor bypass every other motion-confirmed conveyor has is missing. Q-C09.
- **Δ3 (−) on C7** — no running feedback of any kind exists for this machine in the exported IO.
  Q-C33.
- **Δ4 (−) on C14/C16** — the drive's error and condition reports have no signals in the exported IO.
  Q-C30.
- **Δ5 (Δ) on P1/P2/P3** — permitted by "ready and not faulted" rather than by the standard enable
  relation; whether the enable is additionally required is open. Q-C35.
- **Δ6 (−) on C7** — no reverse operation specified (Q-A12); consistent with Δ3.

**DELTAS: none-further-found** — searched: rung-A delta block for this instance, `vsd-motor`
reference §"Class requirement set" and §"Class deltas", rung-B behaviours scoped to this instance
(B-02, 05, 08, 09, 11, 12, 14, 15, 19, 20, 21, 24, 27, 28, 29, 34, 35, 36, 39, 40, 45, 46, 50, 51,
52), the §8a sweep above, the §8b plant residual list, and a field comparison against `MotorVSDInst1`
(which produced the differing hand-speed and fault-time settings).

## OPEN

**Blocking:** Q-C04, Q-C05, Q-C09, Q-C13, Q-C14, Q-C15, Q-C17, Q-C25, Q-C30, Q-C33, Q-C34, Q-C35.
**Carried, still blocking:** Q-A01, Q-A03, Q-A10, Q-A13, Q-B01, Q-B09, Q-B12, Q-B17.
**Non-blocking:** Q-C32, Q-A12, Q-A14 (existence now settled by the sweep; use still open under
Q-C09), Q-A15, Q-B21.
