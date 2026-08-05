# Equipment class reference — `vsd-motor`

> **DERIVED FROM AS-BUILT CODE, NOT FROM AN ENGINEERING STANDARD.**
> This file was reverse-derived on 2026-08-04 from the actual interface and logic of the given FB
> type `MotorVSDSystem` (`ir/PlantAutoControl-bench/MotorVSDSystem.ir`, block NUMBER 42). It is **not** a site
> standard, a vendor document, or a reviewed engineering reference. It records "what this class of
> equipment is observed to require, as built", which is a strictly weaker claim than "how a VSD
> motor is normally controlled".
>
> **Consequence for every rung that consumes it:** completeness-by-construction is only as good as
> this file. A standard requirement the as-built FB happens *not* to implement is invisible here and
> downstream. Recorded as blocking Q-01 in `gen/PlantAutoControl-bench-rerun/equipment-topology.md`.

- **Class:** `vsd-motor`
- **FB type:** `MotorVSDSystem`
- **Derived from:** `ir/PlantAutoControl-bench/MotorVSDSystem.ir` — INTERFACE (`IO : "MotorVSDIOSet"`) and
  networks 1–16.
- **Known instances in this plant:** `MotorVSDInst1`, `MotorVSDInst2`, `MotorVSDInst3`.
- **Version:** v0-derived (2026-08-04).

## Class requirement set (process abstraction)

| ID | Requirement (process language) |
|---|---|
| VSD-01 | The motor runs on an automatic start command from the plant, unless the operator has taken it to hand, in which case it runs on the operator's hand start command instead. |
| VSD-02 | The motor does not start while it is inhibited (isolated / locked off). |
| VSD-03 | The motor does not start while it has an active fault, and does not start while a fail-to-run fault is standing. |
| VSD-04 | The motor does not start unless it is reported system-healthy. |
| VSD-05 | The motor does not start until the plant pre-start warning phase has completed. |
| VSD-06 | The motor runs at a speed setpoint: an automatic setpoint while in auto, an operator setpoint while in hand, in both cases floored at a minimum running speed and delivered to the drive as a speed demand scaled against the machine's rated maximum speed. |
| VSD-07 | The motor can be commanded to run in reverse as well as forward; its running state is confirmed from a forward running feedback or a reverse running feedback. |
| VSD-08 | Once running and confirmed running for a configurable up-to-speed time, the motor declares itself enabled, which is what permits the machine it serves to start. |
| VSD-09 | On a controlled shutdown request the motor stops, and declares its shutdown complete once a configurable shutdown time has elapsed and both running feedbacks have cleared. |
| VSD-10 | The motor also declares shutdown complete immediately if it is faulted. |
| VSD-11 | Run-confirmation supervision is not armed until a configurable start-up time has elapsed after the drive is commanded and its speed demand has settled, and it is re-armed whenever the direction changes — so a ramp or a direction reversal does not itself raise a fault. |
| VSD-12 | After that start-up allowance, if the motor is commanded but does not report running within a configurable fault time, a fail-to-run fault is raised and latched. |
| VSD-13 | If the motor reports running while not commanded for that same fault time, a fail-to-stop fault is raised and latched. |
| VSD-14 | A drive error reported by the VSD raises a latched fault. |
| VSD-15 | A fault-reset command clears all latched faults. |
| VSD-16 | The drive's own condition is brought back for the operator: ready, operation-enabled, warning, over-speed, under-speed, over-temperature, thermal overload, torque-limit-OK, motor current, speed feedback and speed-reached. |
| VSD-17 | A motion/rotation sensor input exists on this class; whether an instance uses it, and for what, is an instance-level decision (see deltas at rung A). |
| VSD-18 | Running hours are totalised for the motor. |
| VSD-19 | A status indication is published for the operator (faulted / stopped / commanded / running / enabled). |
| VSD-20 | An alarm indication is published for: fail-to-run, fail-to-stop, and drive error. |

## As-built provenance (rung-C material — do NOT consume above rung C)

| ID | As-built origin in `MotorVSDSystem.ir` |
|---|---|
| VSD-01 | `IO.InHand`, `IO.HandIntervention`, `IO.AutoStartSignal`, `IO.HandStartSignal` — network 1 |
| VSD-02 | `IO.InhibitMotor` — network 1 |
| VSD-03 | `IO.FaultActive`, `IO.FTR` — network 1 |
| VSD-04 | `IO.SystemHealthy` — network 1 |
| VSD-05 | `IO.PreStartDone` → `PreStartMemory` — networks 1, 5 |
| VSD-06 | `IO.AutoSpeedInput`, `IO.HandSpeedInput`, `IO.SpeedPerc`, `IO.SpeedOutput`, `IO.MaxRPM`, `MinSpd` (CONSTANT 20.0), `CALL AnalogScale` — network 6 |
| VSD-07 | `IO.RunRev`, `IO.RunningFwdFB`, `IO.RunningRevFB` — networks 8, 9, 13 |
| VSD-08 | `IO.UPSEnable`, `IO.EnableUPSTime`, `EnableUpstreamTimer` — network 13 |
| VSD-09 | `IO.Shutdown`, `IO.StopMotor`, `IO.ShutdownTime`, `IO.ShutdownComplete` — network 8 |
| VSD-10 | `IO.ShutdownComplete` OR-term on `IO.FaultActive` — network 8 |
| VSD-11 | `StartUpTimer`, `IO.StartUpTime` (=5.0), `RevPosEgde`/`RevNegEge` — network 9 |
| VSD-12 | `IO.FTR`, `IO.FTTime`, `FaultTripTimer` gated by `StartUpTimer.Q` — network 10 |
| VSD-13 | `IO.FTS`, `FTSTimer` — network 11 |
| VSD-14 | `IO.VSDError` — network 12 |
| VSD-15 | `IO.FaultReset` — networks 10, 11, 12 |
| VSD-16 | `IO.VSDReady`, `VSDOpEnable`, `VSDWarning`, `VSDOverSpeed`, `VSDUnderSpeed`, `OverTempFault`, `ThermalOLFault`, `TorqueLimitOk`, `AmpsFB`, `SpeedFB`, `SpeedReached` — interface only, not consumed inside this FB |
| VSD-17 | `IO.RotationSensor` — interface only, **not consumed inside this FB**; any use is in the calling layer |
| VSD-18 | `IO.HrsRun`, `HrTotaliserTimer` — network 14 |
| VSD-19 | `IO.Telemetry` — network 15 |
| VSD-20 | `IO.Alarm.%X0..%X2` — network 16 |

## Class deltas against the sibling classes (observed, informational)

- **Has no** remote/local permissive (unlike `FilterUnitSystem`).
- **Has** a speed reference, a reverse direction, a start-up (ramp) allowance before fail-to-run
  supervision arms, and a rotation-sensor input member — none of which `FilterUnitSystem` has.
- **Note (carried to rung A/C):** the rotation-sensor member is declared on the class but never read
  inside the class. Any instance that relies on it relies on the *calling* layer, which makes it an
  instance delta, not a class behaviour.
