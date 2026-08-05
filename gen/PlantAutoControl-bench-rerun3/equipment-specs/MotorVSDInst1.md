# EQUIPMENT SPEC — MotorVSDInst1 (Discharge Conveyor VSD)

    class: vsd-motor (reference v0-derived, 2026-08-04)
    fed-by: not-stated-by-source [A, Q-A4]      discharges-to: not-stated-by-source [A, Q-A4]
    serves: Air-Separator VSD (on both of that machine's start paths — not bypassable) [A]

Signal family for this instance: `AirStarDC*` (`MotorVSDInst1.ir` declares `DB MotorVSDInst1`).
Distinct from `AirStarFC*` (the air-separator **feed** conveyor) and `AirStar*` (the air separator
itself) — three neighbouring families that differ by two characters.

## IO BINDING [io]

| Role | Signal | Status |
|---|---|---|
| isolation feedback | `DiscreteInputs.AirStarDCIsoFB` | EXISTS |
| motion / rotation sensor | `DiscreteInputs.AirStarDCRotSen` | EXISTS |
| operator bypass of the motion check | `HMIControlSignals.BypassAirStarDCRotSen` | EXISTS (RETAIN) |
| running-forward confirmation | `DiscreteInputs.AirStarDCRotSen` (**the motion sensor is the source**) [B-23] | EXISTS |
| running-reverse confirmation | **NO SIGNAL** — see C7 / Q-C16 | — |
| drive error | **NO SIGNAL** in the discrete IO — carried on the drive interface | Q-C17 |
| start command out | **NO SIGNAL** — no `AirStarDCStart` in `DiscreteOutputs` | **Q-C08** |
| plant fault-reset command in | `HMIControlSignals.SystemReset` | EXISTS |
| pre-start-complete condition | `PlantControl.PreStartComplete` | EXISTS |
| plant run state | `PlantControl.Status` | EXISTS |
| general sub-system enable | **disputed whether it applies** | **Q-C11** |

`converter tagstatus` evidence: `AirStarDCIsoFB`, `AirStarDCRotSen`, `BypassAirStarDCRotSen` all
EXISTS; `DiscreteOutputs.AirStarDCStart` → **MEMBER-NOT-FOUND**. A full
`candidate-scan --scope DiscreteOutputs --direction command` (28 IO candidates, the entire output DB)
contains no signal of this instance's family at all — so the absence is established by enumeration,
not by one failed guess.

**The two-signal family check.** `candidate-scan --scope DiscreteInputs.AirStarDC` returned exactly
**2** same-typed IO signals facing 28 FB members of matching direction — the transposition signature
the tool exists to surface (two requirements, two same-shaped signals). It is discharged here, not
waved past: `AirStarDCIsoFB` and `AirStarDCRotSen` map to disjoint roles by name, and each has its
own independent documentary citation (REQ-010/B-25 for the isolator, REQ-012/B-23 for the motion
sensor, the latter naming this exact machine). Basis cited; choice recorded as having existed.

## CONTROL REQUIREMENTS (complete by construction from the class reference)

- **C1** the motor runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead [ref VSD-01]
- **C2** the motor does not start while it is inhibited (isolated / locked off)
  [ref VSD-02 + B-25 + io `DiscreteInputs.AirStarDCIsoFB`]
- **C3** the motor does not start while it has an active fault, and does not start while a
  fail-to-run fault is standing [ref VSD-03]
- **C4** the motor does not start unless it is reported system-healthy [ref VSD-04]
  → no signal; health is supervised outside this layer [B-32]. Delta. Q-C20.
- **C5** the motor does not start until the plant pre-start warning phase has completed
  [ref VSD-05 + io `PlantControl.PreStartComplete`]
- **C6** the motor runs at a speed setpoint — an automatic setpoint in auto, an operator setpoint in
  hand — floored at a minimum running speed and delivered to the drive as a demand scaled against
  the machine's rated maximum speed [ref VSD-06]
  → **no plant IO carries any of these**: the discrete IO tables hold no analog speed signal for this
  instance. The speed path lives entirely on the equipment interface (a declared given boundary).
  Recorded, not dropped; values at SETTINGS.
- **C7** the motor can be commanded to run in reverse as well as forward; its running state is
  confirmed from a forward running feedback or a reverse running feedback [ref VSD-07]
  → **forward** is bound to the motion sensor (C11). **Reverse has no signal and no reverse command
  output**: neither a `*RunRev` output nor a reverse running input exists in this instance's family.
  Either this instance has no reverse capability (a MINUS delta) or its reverse path is unsourced.
  **Q-C16 (BLOCKING)** — not resolved by assuming a conveyor "obviously" does not reverse; the
  shredder feed conveyor in this same plant does.
- **C8** once running and confirmed running for the up-to-speed time, the motor declares itself
  enabled, and that enable is what permits the machine it serves to start [ref VSD-08]
- **C9** on a controlled shutdown request the motor stops, and declares its shutdown complete once
  the shutdown time has elapsed and both running feedbacks have cleared [ref VSD-09]
- **C10** the motor also declares shutdown complete immediately if it is faulted [ref VSD-10]
- **C11** the motor's running-forward confirmation is taken from its motion sensor — motion sensed
  means running — and while the operator bypass is set that sensor is **not used at all**
  [ref VSD-17 + B-23 + io `DiscreteInputs.AirStarDCRotSen`,
  `HMIControlSignals.BypassAirStarDCRotSen`]
  → **consequence, recorded because it is easy to miss:** this instance has *no other* running
  feedback signal, so while the bypass is set the running-forward confirmation has **no source at
  all**. The source states the bypass behaviour explicitly (nothing is inferred here); what it does
  not state is whether losing all running confirmation is the intended result. **Q-C18
  (non-blocking).**
- **C12** run-confirmation supervision is not armed until the start-up time has elapsed after the
  drive is commanded and its demand has settled, and is re-armed whenever the direction changes, so
  a ramp or a reversal does not itself raise a fault [ref VSD-11]
- **C13** after that allowance, if the motor is commanded and does not report running within the
  fault time, a fail-to-run fault is raised and latched [ref VSD-12]
- **C14** if the motor reports running while not commanded for that same fault time, a fail-to-stop
  fault is raised and latched [ref VSD-13]
- **C15** a drive error reported by the drive raises a latched fault [ref VSD-14]
  → no plant IO carries a drive error for this instance; it arrives on the drive interface.
  **Q-C17 (non-blocking)** — recorded as an interface-carried input, not as an absent requirement.
- **C16** a fault-reset command clears all latched faults, taken from the single plant-wide operator
  reset [ref VSD-15 + B-15 + io `HMIControlSignals.SystemReset`]
  → see also the plant-level `DiscreteOutputs.VSDFaulrReset` residual, Q-C21: an unclaimed
  drive-reset **output** exists at plant level and may belong to this requirement.
- **C17** the drive's own condition is brought back for the operator: ready, operation-enabled,
  warning, over-speed, under-speed, over-temperature, thermal overload, torque-limit-OK, motor
  current, speed feedback, speed-reached [ref VSD-16]
  → no plant IO; interface-carried (given boundary).
- **C18** a motion/rotation sensor input exists on this class, and **this instance uses it** — see
  C11 [ref VSD-17 + A delta + B-23]
- **C19** running hours are totalised for the motor [ref VSD-18]
- **C20** a status indication is published for the operator [ref VSD-19]
- **C21** *(alarm indication — fail-to-run, fail-to-stop, drive error)* [ref VSD-20]
  → **out of scope for this artifact** (alarms are a separate artifact).
- **C22** the resulting run command reaches the drive. **No physical start output exists for this
  instance**, and the source names it in neither the discrete-output list nor the drive-interface
  list, so the command path is unspecified. **BLOCKING Q-C08.** [B-29, B-28]

## PLANT INTERLOCKS (fully enumerated, one relation per line)

- **P1** start permitted only while the Overband Magnet (`MotorStarterInst8`) is enabled [A rel-13, B-08]
- **P2** start permitted only while the Equipment Control System (`ECSControlInst1`) is enabled
  [A rel-14, B-08]
- **P3** on controlled shutdown, hold until the Air-Separator VSD (`AirStarInst1`) reports its
  shutdown complete [A rel-15, B-05]
- **P4** this machine's enable is a permissive for the Air-Separator VSD's start, on **both** of that
  machine's alternative start paths [A rel-16, B-12]
- **P5** the Overband Magnet holds its controlled shutdown until this machine reports its shutdown
  complete [A rel-17]
- **P6** the Equipment Control System holds its controlled shutdown until this machine reports its
  shutdown complete [A rel-17]
- **P7** while the plant is in its running state, this machine is commanded to start and run
  automatically, subject to P1–P3 and C1–C8 [B-02 + io `PlantControl.Status`]
- **P8** while this machine is under hand intervention, its automatic controlled-shutdown request is
  suppressed [B-13]
- **P9** **DISPUTED** — start permitted only while the general sub-system enable is present
  [B-11 + io `PlantControl.GeneralEnable`] **vs** no sub-system-enable gate at all [A row 6].
  **BLOCKING Q-C11.** Recorded as a disputed relation with both readings, per §5; **not** chosen.

## SETTINGS

| Setting | Owner | Value | Basis |
|---|---|---|---|
| up-to-speed (enable) time | engineering | **not stated** | Q-C15 |
| shutdown-complete time | engineering | **not stated** | Q-C15 |
| fail-to-run / fail-to-stop time | engineering | **not stated** | Q-C15 |
| drive start-up (ramp) allowance | engineering | **not stated by the plant sources** | the class reference records a value on the interface; not restated as a plant setting here, and not invented |
| automatic speed setpoint | HMI / engineering | **not stated** | Q-C15 |
| hand speed setpoint | HMI | **not stated** | Q-C15 |
| minimum running speed | engineering | **not stated** | Q-C15 |
| rated maximum speed | engineering | **not stated** | Q-C15 |
| motion-check bypass | HMI | operator-set, retentive | `HMIControlSignals.BypassAirStarDCRotSen` [io] |

## UNCLAIMED IO (§8(a) per-instance sweep)

| Signal | Disposition |
|---|---|
| `DiscreteInputs.AirStarDCIsoFB` | bound — C2 |
| `DiscreteInputs.AirStarDCRotSen` | bound — C11 / C18 |
| `HMIControlSignals.BypassAirStarDCRotSen` | bound — C11 |
| `PlantControl.GeneralEnable` | **disputed claim** — P9 / Q-C11 |
| `DiscreteOutputs.VSDFaulrReset` | **UNCLAIMED at instance level — raised at plant level, Q-C21.** A drive fault-reset output claimed by no instance spec; it may belong to C16 here, to `MotorVSDInst3`, to both, or to drives outside this run's scope. Not claimed unilaterally. |

No signal of this instance's family is left unswept: `DiscreteInputs.AirStarDC*` is exactly
{`IsoFB`, `RotSen`} (candidate-scan), `HMIControlSignals.*AirStarDC*` is exactly
{`BypassAirStarDCRotSen`}, and `DiscreteOutputs.*AirStarDC*` is empty.

## DELTAS

- **PLUS** — takes its running-forward confirmation from a motion sensor rather than a drive run
  feedback [A, B-23]. Per the class reference the class never consumes that input internally, so the
  use is in the calling layer: an instance delta, not class behaviour.
- **MINUS (new at this rung)** — has **no running-feedback signal of any kind** other than the
  motion sensor, so the bypass at C11 leaves running confirmation sourceless → Q-C18.
- **MINUS (new at this rung)** — has **no discrete start output**, and no documented drive-interface
  command path either → Q-C08.
- **MINUS (new at this rung)** — has **no reverse command output and no reverse running input**,
  while its class carries a reverse capability → Q-C16.
- **DISPUTED (new at this rung)** — whether the general sub-system enable gates this machine → Q-C11.
- **NONE-FURTHER-FOUND — searched:** rung-A deltas; class reference §Class requirement set
  (VSD-01..VSD-20), §Class deltas and the VSD-17 note; §8(a) sweep above; §8(b) plant residual
  sweep; `candidate-scan` on `DiscreteInputs.AirStarDC` (2 IO) and on the whole of `DiscreteOutputs`
  (28 IO); `tagstatus` on every bound and attempted name.

## OPEN

- **Q-C08 (BLOCKING)** — no command path for this machine's run command (no output, not in the
  drive-interface list either)
- **Q-C11 (BLOCKING)** — does the general sub-system enable gate this machine? (carries Q-B2)
- **Q-C16 (BLOCKING)** — reverse capability: absent on this instance, or unsourced?
- **Q-C18 (non-blocking)** — is losing all running confirmation while the motion check is bypassed
  the intended result?
- **Q-C17 (non-blocking)** — drive error and drive condition set are interface-carried, not plant IO
- **Q-C21 (BLOCKING, plant-level)** — `DiscreteOutputs.VSDFaulrReset` claimed by no instance
- **Q-C15 (non-blocking)** — eight settings with no stated value
- **Q-C20 (BLOCKING, plant-level)** — plant-health residual signals (C4 has no source)
