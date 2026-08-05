# Equipment class reference — `optical-sorter`

> **DERIVED FROM AS-BUILT CODE, NOT FROM AN ENGINEERING STANDARD.**
> This file was reverse-derived on 2026-08-04 from the actual interface and logic of the given FB
> type `TomraControlSystem` (`ir/PlantAutoControl-bench/TomraControlSystem.ir`, block NUMBER 5). It is **not** a
> site standard, a vendor document, or a reviewed engineering reference. It records "what this class
> of equipment is observed to require, as built", which is a strictly weaker claim than "how an
> optical sorter is normally controlled".
>
> **This class has exactly one instance in this plant** (`TomraControlInst1`), so "class" and
> "instance" cannot be separated by observation: anything peculiar to that one machine is
> indistinguishable from a class property. Recorded as blocking Q-02 in
> `gen/PlantAutoControl-bench-rerun/equipment-topology.md`.

- **Class:** `optical-sorter`
- **FB type:** `TomraControlSystem`
- **Derived from:** `ir/PlantAutoControl-bench/TomraControlSystem.ir` — INTERFACE and networks 1–20.
- **Known instances in this plant:** `TomraControlInst1`.
- **Version:** v0-derived (2026-08-04).

## Class requirement set (process abstraction)

| ID | Requirement (process language) |
|---|---|
| OS-01 | The sorter runs on an automatic start command from the plant, unless the operator has taken it to hand, in which case it runs on the operator's hand start command instead. |
| OS-02 | The sorter does not start while it is inhibited (isolated / locked off). |
| OS-03 | The sorter does not start while it has an active fault, and does not start while a fail-to-run fault is standing. |
| OS-04 | The sorter does not start until the plant pre-start warning phase has completed. |
| OS-05 | The sorter is commanded both over a data link to the machine and by a hardwired run signal; both are driven together from the same run decision. |
| OS-06 | The sorter's condition is read both over the data link and from hardwired ready / running / communications-fault signals. |
| OS-07 | The data link is supervised by a liveness indication from the sorter. If liveness is lost for a fixed watchdog period, a communications-lost condition is latched until reset. |
| OS-08 | While the data link is healthy, the sorter counts as running only when both the link and the hardwired running signal say so; while the link is dead, the hardwired running signal alone decides. The same fallback applies to the sorter's communications-fault indication. |
| OS-09 | Once running for a configurable up-to-speed time (and while the sorter reports itself ready), the sorter declares itself enabled, which is what permits the machine it serves to start. It withdraws that enable immediately if it stops running. |
| OS-10 | On a controlled shutdown request the sorter stops, and declares its shutdown complete once a configurable shutdown time has elapsed and it has stopped running. |
| OS-11 | If the sorter is commanded to run but does not report running within a configurable fault time, a fail-to-run fault is raised and latched. |
| OS-12 | If the sorter reports running while not commanded for that same fault time, a fail-to-stop fault is raised and latched. |
| OS-13 | Any of the sorter's own reported machine faults, plus loss of the data link, raises a latched fault. |
| OS-14 | A fault-reset command clears all latched faults and is also passed to the sorter itself over the data link. |
| OS-15 | Running hours are totalised for the sorter. |
| OS-16 | A status indication is published for the operator (faulted / stopped / commanded / running / enabled). |
| OS-17 | A wide alarm indication is published, covering the sorter's own reported fault set as well as fail-to-run, fail-to-stop and loss of the data link. |
| OS-18 | The sorting program to run and the sorter's belt on/off delays are operator-settable. |
| OS-19 | The byte order of the data exchanged with the sorter is selectable at the plant level, to suit the machine's actual word ordering. |

## As-built provenance (rung-C material — do NOT consume above rung C)

| ID | As-built origin in `TomraControlSystem.ir` |
|---|---|
| OS-01 | `Inputs.InHand`, `HandIntervention`, `AutoStartSignal`, `HandStartSignal` — networks 6, 7 |
| OS-02 | `Inputs.InhibitMotor` — network 6 |
| OS-03 | `Outputs.FaultActive`, `Outputs.FTR` — network 6 |
| OS-04 | `Inputs.PreStartDone` → `PreStartMemory` — networks 6, 10 |
| OS-05 | `ControlWord0.%X0` and `HardWireSignals.Run`, both driven in network 6 |
| OS-06 | `inputWord0..7` → `StatusWord0..7` (network 1); `HardWireSignals.Ready/Running/ComError` |
| OS-07 | `StatusWord0.%X10` life bit, `LifeBitTimer` PT `T#5S`, `LostLifeBit` — network 5 |
| OS-08 | `Outputs.OSRunning` (network 4), `Outputs.OSComFault` (network 2) |
| OS-09 | `Outputs.UPSEnable`, `Inputs.EnableUPSTime`, `EnableUpstreamTimer` gated `StatusWord0.%X0` — network 16 |
| OS-10 | `Inputs.Shutdown`, `Outputs.Stop`, `Inputs.ShutdownTime`, `Outputs.ShutdownComplete` — network 12 |
| OS-11 | `Outputs.FTR`, `Inputs.FTTime`, `FaultTripTimer` — network 13 |
| OS-12 | `Outputs.FTS`, `FTSTimer` — network 14 |
| OS-13 | `Outputs.FaultActive` — network 15 (14 status bits ORed with `LostLifeBit`) |
| OS-14 | `Inputs.FaultReset`; echoed to `ControlWord0.%X7` — network 3 |
| OS-15 | `Outputs.HrsRun`, `HrTotaliserTimer` — network 17 |
| OS-16 | `Outputs.Telemetry` — network 18 |
| OS-17 | `Outputs.Alarm0/1/2` — network 19 |
| OS-18 | `Inputs.ProgramSelection`, `Belt1OnDelay`, `Belt1OffDelay` — interface only |
| OS-19 | `PlantControl.Test[5]` selecting `MOVE` vs `SWAP` — network 20 |

## Class deltas against the sibling classes (observed, informational)

- **Has no** system-healthy permissive (`FilterUnitSystem` FU-04 / `vsd-motor` VSD-04 have one). The
  common start-permissive set is one condition **shorter** for this class.
- **Has no** remote/local permissive.
- Its shutdown-complete has **no** fault or hand escape term — unlike `FilterUnitSystem` (FU-09) and
  `vsd-motor` (VSD-10), a faulted sorter does **not** free the machine that is waiting on its
  shutdown-complete. Whether that is intended is not derivable from code.
- Its enable is **withdrawn immediately** on loss of running (OS-09), unlike the sibling classes
  whose enable falls only with the timer input.

## Observed as-built anomalies (recorded, NOT stated as requirements)

Derivation from code cannot distinguish an intention from a mistake. These are logged so no
downstream rung silently inherits them as requirements:

- **A-1** Network 2 both **reads and writes** `StatusWord0.%X2`: the comms-fault output is computed
  from it, and then the same bit is overwritten from a plant test/simulation array. The value the
  fault logic used is therefore last scan's.
- **A-2** Network 7 computes the hand start signal as *in-hand AND not-hand-intervention AND
  fault-active* with a plain coil, where both sibling classes use a reset coil with a different
  condition. As written, a hand start signal can only be set while the machine is faulted.
- **A-3** `Inputs.ProgramSelection` and `Inputs.Belt1OnDelay` are declared but never used; the
  scaled `Belt1OnDelayMS` is never computed while `Belt1OffDelayMS` is. `ControlWord1` is
  transmitted but never written. OS-18 is therefore an *interface* promise, not an implemented one.
