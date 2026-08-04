# Equipment class reference — `FilterUnitSystem`

> **DERIVED FROM AS-BUILT CODE, NOT FROM AN ENGINEERING STANDARD.**
> This file was reverse-derived on 2026-08-04 from the actual interface and logic of the given FB
> type `FilterUnitSystem` (`ir/PlantAutoControl-bench/FilterUnitSystem.ir`, block NUMBER 8). It is **not** a
> site standard, a vendor document, or a reviewed engineering reference. It records "what this
> class of equipment is observed to require, as built", which is a strictly weaker claim than "how
> a filter unit is normally controlled".
>
> **Consequence for every rung that consumes it:** completeness-by-construction is only as good as
> this file. A standard requirement that the as-built FB happens *not* to implement is invisible
> here and will be invisible downstream. This is recorded as a blocking open question in
> `gen/PlantAutoControl-bench-rerun/equipment-topology.md` (Q-01).

- **Class:** `FilterUnitSystem`
- **FB type:** `FilterUnitSystem`
- **Derived from:** `ir/PlantAutoControl-bench/FilterUnitSystem.ir` — INTERFACE (`IO : "MotorIOSet"`, `RemoteOp`)
  and networks 1–14.
- **Known instances in this plant:** `FilterUnitInst1`, `FilterUnitInst2`, `FilterUnitInst3`.
- **Version:** v0-derived (2026-08-04).

## Class requirement set (process abstraction)

Every instance of this class inherits all of these. An instance that does not have one is a
**minus-delta** and must be recorded as such at rung A.

| ID | Requirement (process language) |
|---|---|
| FU-01 | The unit runs on an automatic start command from the plant, unless the operator has taken it to hand, in which case it runs on the operator's hand start command instead. |
| FU-02 | The unit does not start while it is inhibited (isolated / locked off). |
| FU-03 | The unit does not start while it has an active fault. |
| FU-04 | The unit does not start unless it is reported system-healthy. |
| FU-05 | The unit does not start unless it is in remote (not local) operating mode. *This permissive is specific to this class — the motor classes do not have it.* |
| FU-06 | The unit does not start until the plant pre-start warning phase has completed. |
| FU-07 | Once running and confirmed running for a configurable up-to-speed time, the unit declares itself enabled, which is what permits the machine it serves to start. |
| FU-08 | On a controlled shutdown request the unit stops, and declares its shutdown complete once a configurable shutdown time has elapsed and its running feedback has cleared. |
| FU-09 | The unit also declares shutdown complete immediately if it is faulted, or if it is under hand control and the operator is not calling it to run. |
| FU-10 | If the unit is commanded to run but does not report running within a configurable fault time, a fail-to-run fault is raised and latched. |
| FU-11 | If the unit reports running while not commanded to run for that same fault time, a fail-to-stop fault is raised and latched. |
| FU-12 | A fault feedback from the unit itself raises a latched fault. |
| FU-13 | A fault-reset command clears all latched faults. |
| FU-14 | The unit's actual running state is taken from its own running feedback. This class has no motion/rotation sensor. |
| FU-15 | Running hours are totalised for the unit. |
| FU-16 | A status indication is published for the operator (faulted / stopped / commanded / running / enabled). |
| FU-17 | An alarm indication is published for: fail-to-run, fail-to-stop, unit fault feedback, and unit not in remote. |

## As-built provenance (rung-C material — do NOT consume above rung C)

Interface members are named here only so rung C can bind signals to requirements. Rungs A and B
must not read this section.

| ID | As-built origin in `FilterUnitSystem.ir` |
|---|---|
| FU-01 | `IO.InHand`, `IO.HandIntervention`, `IO.AutoStartSignal`, `IO.HandStartSignal` — network 1 |
| FU-02 | `IO.InhibitMotor` — network 1 |
| FU-03 | `IO.FaultActive` — network 1 |
| FU-04 | `IO.SystemHealthy` — network 1 |
| FU-05 | `RemoteOp` (STATIC SETPOINT) — network 1 |
| FU-06 | `IO.PreStartDone` → `PreStartMemory` — networks 1, 5 |
| FU-07 | `IO.UPSEnable`, `IO.EnableUPSTime` via `EnableUpstreamTimer` — network 11 |
| FU-08 | `IO.Shutdown`, `IO.StopMotor`, `IO.ShutdownTime`, `IO.ShutdownComplete` — network 7 |
| FU-09 | `IO.ShutdownComplete` OR-terms on `IO.FaultActive` / hand — network 7 |
| FU-10 | `IO.FTR`, `IO.FTTime`, `FaultTripTimer` — network 8 |
| FU-11 | `IO.FTS`, `FTSTimer` — network 9 |
| FU-12 | `IO.FaultFB` — network 10 |
| FU-13 | `IO.FaultReset` — networks 8, 9, 10 |
| FU-14 | `IO.RunningFB` — networks 7, 8, 9, 11 |
| FU-15 | `IO.HrsRun`, `HrTotaliserTimer` — network 12 |
| FU-16 | `IO.Telemetry` — network 13 |
| FU-17 | `IO.Alarm.%X0..%X3` — network 14 |

## Class deltas against the sibling motor classes (observed, informational)

- **Has** a remote/local permissive (FU-05) that `vsd-motor` and `optical-sorter` do not.
- **Has no** direction/reverse capability and **no** speed reference.
- **Has no** rotation/motion sensor input (FU-14).
