# PlantAutoControl-bench-rerun2 — Requirements Register (derived view)

**Derived, not authored.** This register is a mechanical view of the `C-nn` / `P-nn` relation sets in
`gen/PlantAutoControl-bench-rerun2/equipment-specs/`. It exists because `review-functional` **stops
without a register** — a project specified through the A→B→C→D rungs cannot be functionally reviewed
at all unless this view exists. It adds no content: every row traces to exactly one spec relation.

## Provenance

- **Produced:** 2026-08-05, `gen-equipment-spec` (rung C), run `PlantAutoControl-bench-rerun2`.
- **Derived from:** the six specs in `gen/PlantAutoControl-bench-rerun2/equipment-specs/` —
  `FilterUnitInst2.md`, `FilterUnitInst3.md`, `FilterUnitInst1.md`, `MotorVSDInst1.md`,
  `MotorVSDInst3.md`, `TomraControlInst1.md`.
- **Upstream chain:** rung A `equipment-topology.md` ← rung B `plant-behaviours.md` ← the source
  register `gen/PlantAutoControl-bench/requirements.md` (SHA-256 `c0de9b79…46ecba`) and the three class
  references in `references/`.
- **Scope:** six of the plant's twenty machines. The other fourteen are **not** specified and have
  **no** rows here.
- **Tag status:** every signal named in the specs was **grep-verified member-by-member** against
  `ir/PlantAutoControl-bench/*.ir`. `converter tagstatus --project ir/PlantAutoControl-bench` was run over the
  bound set and reported **31 names, 0 proposed** — but **that result is not the basis of any claim
  here**: the tool validates only the root DB name and reports invented members of real DBs as
  `EXISTS` (verified: `HMIControlSignals.TotallyInventedMember -> EXISTS`, exit 0 for that name).
  The **proposed** signals below were found by grep, not by the tool.
- **Proposed (never coded against, hard rule 3):** `HMIControlSignals.BypassOSCRotSen`;
  `Tag_45`…`Tag_54` (the optical sorter's 8-in/2-out data-link words); a per-machine hand/auto mode
  source for all six instances; an isolator feedback for all three filter units and the optical
  sorter; forward/reverse running feedbacks for both in-scope VSDs; drive error/condition reports
  for both in-scope VSDs.
- **Safety:** no F-block, F-runtime group or safety-program content was read, written, converted,
  explained or referenced. E-stop feedback members present in the given DBs are listed by name only
  in `unclaimed-signals.md` and are bound by nothing here. Hard rule 2 not triggered.

## Completeness by set-difference

| | Count |
|---|---|
| `C-nn` / `P-nn` relation ids across all six specs | **174** |
| `REQ-nnn` rows in this register | **174** |
| Relations in the specs with no REQ row (**must be 0**) | **0** |
| REQ rows with no spec relation (**must be 0**) | **0** |

Per instance: `FilterUnitInst2` C17+P10=27 → REQ-001…027 · `FilterUnitInst3` C17+P10=27 →
REQ-028…054 · `FilterUnitInst1` C17+P10=27 → REQ-055…081 · `MotorVSDInst1` C20+P10=30 →
REQ-082…111 · `MotorVSDInst3` C20+P14=34 → REQ-112…145 · `TomraControlInst1` C19+P10=29 →
REQ-146…174. Sum 27+27+27+30+34+29 = **174**. ✓

**Format deviation, declared:** `gen/test-project001/requirements.md` uses a heading block per REQ.
At 174 rows that form carries no information the table below lacks and would be unusable as a trace
target, so this view is tabular. Every field that form requires — id, text, class, source/provenance,
tag status, open question — is present per row.

## Classes

`control` sequencing/interlocks/drives · `mode` auto/hand/enable semantics · `HMI` operator
commands/indications · `timing` named delays/setpoints · `alarm` fault annunciation (recorded, out
of scope for a control spec) · `out-of-scope` other-block or hardwired material.

## Requirements

### `FilterUnitInst2` — Dust Filter Unit 1 (FilterUnitSystem)

| REQ | Rel | Text | Class | Provenance | Open |
|---|---|---|---|---|---|
| REQ-001 | C1 | Runs on the plant automatic start command unless taken to hand, then on the operator's hand start command | mode | ref FU-01 | **Q-C04** |
| REQ-002 | C2 | Does not start while inhibited (isolated / locked off) | control | ref FU-02 | **Q-C03** |
| REQ-003 | C3 | Does not start while it has an active fault — `DiscreteInputs.FilterUnit1Flt` | control | ref FU-03 + io | — |
| REQ-004 | C4 | Does not start unless reported system-healthy | control | ref FU-04 | **Q-C05** |
| REQ-005 | C5 | Does not start unless in remote (not local) operating mode | mode | ref FU-05 | **Q-C01** |
| REQ-006 | C6 | Does not start until the plant pre-start warning phase has completed — `PlantControl.PreStartComplete` | control | ref FU-06 + io | — |
| REQ-007 | C7 | Declares itself enabled after a configurable up-to-speed time, which permits the machine it serves to start — `ProcessTimings.NormalFanStartTime` | control | ref FU-07 + io | — |
| REQ-008 | C8 | On controlled shutdown, stops and declares shutdown complete after a configurable shutdown time with running feedback cleared | control | ref FU-08 | — |
| REQ-009 | C9 | Also declares shutdown complete immediately if faulted, or if in hand and not called to run | control | ref FU-09 | — |
| REQ-010 | C10 | Fail-to-run fault raised and latched if commanded without a running report within the fault time | control | ref FU-10 | — |
| REQ-011 | C11 | Fail-to-stop fault raised and latched if running while not commanded for that fault time | control | ref FU-11 | — |
| REQ-012 | C12 | A fault feedback from the unit raises a latched fault — `DiscreteInputs.FilterUnit1Flt` | control | ref FU-12 + io | — |
| REQ-013 | C13 | A fault-reset command clears all latched faults — `HMIControlSignals.SystemReset` | HMI | ref FU-13 + io | — |
| REQ-014 | C14 | Running state taken from the unit's own running feedback; no motion sensor on this class | control | ref FU-14 | **Q-C01** |
| REQ-015 | C15 | Running hours totalised | HMI | ref FU-15 | — |
| REQ-016 | C16 | Operator status indication (faulted/stopped/commanded/running/enabled) | HMI | ref FU-16 | — |
| REQ-017 | C17 | Alarm indication for fail-to-run, fail-to-stop, unit fault, not-in-remote | alarm | ref FU-17 | out of control-spec scope |
| REQ-018 | P1 | Start permitted only while the Sorter Conveyor VSD is enabled | control | A + B-08 | — |
| REQ-019 | P2 | On controlled shutdown, hold until the plant reports the filter fans ready to shut down — `PlantControl.FansShutdownReady` | control | A + B-17 + io | **Q-C12** |
| REQ-020 | P3 | On controlled shutdown, hold until the Air-Separator VSD reports shutdown complete | control | A + B-17 | **Q-C12** |
| REQ-021 | P4 | Start permitted only while the plant is in its running state — `PlantControl.Status` | control | A + B-02 + io | — |
| REQ-022 | P5 | Start permitted only after the plant pre-start warning phase has completed | control | A + B-05 + io | — |
| REQ-023 | P6 | While under hand intervention, the automatic controlled-shutdown request is suppressed | mode | A + B-19 | — |
| REQ-024 | P7 | Latched faults cleared by the single plant-wide operator reset | HMI | A + B-24 + io | — |
| REQ-025 | P8 | The plant-wide reset is echoed to the unit as a physical reset command — `DiscreteOutputs.FilterUnit1Reset` | control | A delta + B-25 + io | — |
| REQ-026 | P9 | This unit's enable is a start permissive for the Air-Separator VSD (primary path only) | control | A + B-44 | — |
| REQ-027 | P10 | The run command drives the physical start output — `DiscreteOutputs.FilterUnit1Start` | control | B-40 + io | — |

### `FilterUnitInst3` — Dust Filter Unit 2 (FilterUnitSystem)

| REQ | Rel | Text | Class | Provenance | Open |
|---|---|---|---|---|---|
| REQ-028 | C1 | Runs on the plant automatic start command unless taken to hand, then on the hand start command | mode | ref FU-01 | **Q-C04** |
| REQ-029 | C2 | Does not start while inhibited | control | ref FU-02 | **Q-C03** |
| REQ-030 | C3 | Does not start while it has an active fault — `DiscreteInputs.FilterUnit2Flt` | control | ref FU-03 + io | — |
| REQ-031 | C4 | Does not start unless reported system-healthy | control | ref FU-04 | **Q-C05** |
| REQ-032 | C5 | Does not start unless in remote operating mode | mode | ref FU-05 | **Q-C02** |
| REQ-033 | C6 | Does not start until the plant pre-start warning phase has completed | control | ref FU-06 + io | — |
| REQ-034 | C7 | Declares itself enabled after a configurable up-to-speed time — `ProcessTimings.NormalFanStartTime` | control | ref FU-07 + io | — |
| REQ-035 | C8 | On controlled shutdown, stops and declares shutdown complete after the shutdown time | control | ref FU-08 | — |
| REQ-036 | C9 | Also declares shutdown complete immediately if faulted, or in hand and not called to run | control | ref FU-09 | — |
| REQ-037 | C10 | Fail-to-run fault raised and latched | control | ref FU-10 | — |
| REQ-038 | C11 | Fail-to-stop fault raised and latched | control | ref FU-11 | — |
| REQ-039 | C12 | Unit fault feedback raises a latched fault — `DiscreteInputs.FilterUnit2Flt` | control | ref FU-12 + io | — |
| REQ-040 | C13 | Fault reset clears all latched faults — `HMIControlSignals.SystemReset` | HMI | ref FU-13 + io | — |
| REQ-041 | C14 | Running state from the unit's own running feedback; no motion sensor | control | ref FU-14 | **Q-C02** |
| REQ-042 | C15 | Running hours totalised | HMI | ref FU-15 | — |
| REQ-043 | C16 | Operator status indication | HMI | ref FU-16 | — |
| REQ-044 | C17 | Alarm indication for fail-to-run, fail-to-stop, unit fault, not-in-remote | alarm | ref FU-17 | out of control-spec scope |
| REQ-045 | P1 | Start permitted only while the Sorter Conveyor VSD is enabled | control | A + B-08 | — |
| REQ-046 | P2 | Hold through shutdown until the plant reports the filter fans ready to shut down | control | A + B-17 + io | **Q-C12** |
| REQ-047 | P3 | Hold through shutdown until the Air-Separator VSD reports shutdown complete | control | A + B-17 | **Q-C12** |
| REQ-048 | P4 | Start permitted only while the plant is in its running state | control | A + B-02 + io | — |
| REQ-049 | P5 | Start permitted only after the pre-start warning phase has completed | control | A + B-05 + io | — |
| REQ-050 | P6 | Hand intervention suppresses the automatic controlled-shutdown request | mode | A + B-19 | — |
| REQ-051 | P7 | Latched faults cleared by the plant-wide operator reset | HMI | A + B-24 + io | — |
| REQ-052 | P8 | Reset echoed to the unit as a physical reset command — `DiscreteOutputs.FilterUnit2Reset` | control | A delta + B-25 + io | — |
| REQ-053 | P9 | This unit's enable is a start permissive for the Air-Separator VSD (primary path only) | control | A + B-44 | — |
| REQ-054 | P10 | Run command drives the physical start output — `DiscreteOutputs.FilterUnit2Start` | control | B-40 + io | — |

### `FilterUnitInst1` — Cyclone Filter Unit (FilterUnitSystem)

| REQ | Rel | Text | Class | Provenance | Open |
|---|---|---|---|---|---|
| REQ-055 | C1 | Runs on the plant automatic start command unless taken to hand, then on the hand start command | mode | ref FU-01 | **Q-C04** |
| REQ-056 | C2 | Does not start while inhibited | control | ref FU-02 | **Q-C03** |
| REQ-057 | C3 | Does not start while it has an active fault — fault derived from the absence of `DiscreteInputs.CycloneDustSysOk` | control | ref FU-03 + io + B-30 | — |
| REQ-058 | C4 | Does not start unless reported system-healthy | control | ref FU-04 | **Q-C27** |
| REQ-059 | C5 | Does not start unless in remote operating mode — `DiscreteInputs.CycloneDustRemOp` | mode | ref FU-05 + io | — |
| REQ-060 | C6 | Does not start until the pre-start warning phase has completed | control | ref FU-06 + io | — |
| REQ-061 | C7 | Declares itself enabled after a configurable up-to-speed time — `ProcessTimings.NormalFanStartTime` | control | ref FU-07 + io | **Q-C20** (serves no machine) |
| REQ-062 | C8 | On controlled shutdown, stops and declares shutdown complete after the shutdown time | control | ref FU-08 | — |
| REQ-063 | C9 | Also declares shutdown complete immediately if faulted, or in hand and not called to run | control | ref FU-09 | — |
| REQ-064 | C10 | Fail-to-run fault raised and latched | control | ref FU-10 | — |
| REQ-065 | C11 | Fail-to-stop fault raised and latched | control | ref FU-11 | — |
| REQ-066 | C12 | Unit fault feedback raises a latched fault — **no direct signal; satisfied only via the inverted system-OK report** | control | ref FU-12 + Δ1 | — |
| REQ-067 | C13 | Fault reset clears all latched faults — `HMIControlSignals.SystemReset` | HMI | ref FU-13 + io | — |
| REQ-068 | C14 | Running state from the unit's own running feedback — `DiscreteInputs.CycloneDustAutoRunning/Stop` | control | ref FU-14 + io | **Q-C06** |
| REQ-069 | C15 | Running hours totalised | HMI | ref FU-15 | — |
| REQ-070 | C16 | Operator status indication | HMI | ref FU-16 | — |
| REQ-071 | C17 | Alarm indication for fail-to-run, fail-to-stop, unit fault, not-in-remote | alarm | ref FU-17 | out of control-spec scope |
| REQ-072 | P1 | Start permissive — **NOT ESTABLISHED**; the source records only "(runs with plant)" | control | A | **Q-C19** |
| REQ-073 | P2 | Hold through shutdown until the plant reports the filter fans ready to shut down | control | A + B-17 + io | **Q-C12** |
| REQ-074 | P3 | Hold through shutdown until the Shredder reports shutdown complete | control | A + B-17 | **Q-C12** |
| REQ-075 | P4 | Start permitted only while the plant is in its running state | control | A + B-02 + io | — |
| REQ-076 | P5 | Start permitted only after the pre-start warning phase has completed | control | A + B-05 + io | — |
| REQ-077 | P6 | Hand intervention suppresses the automatic controlled-shutdown request | mode | A + B-19 | — |
| REQ-078 | P7 | Latched faults cleared by the plant-wide operator reset | HMI | A + B-24 + io | — |
| REQ-079 | P8 | Reset echoed to the unit as a physical reset command — `DiscreteOutputs.CycloneDustFilterReset` | control | A delta + B-25 + io | — |
| REQ-080 | P9 | Served machine — **NOT ESTABLISHED** | control | A | **Q-C20** |
| REQ-081 | P10 | Run command drives the physical start output — `DiscreteOutputs.CycloneDustFilterStart` | control | B-40 + io | — |

### `MotorVSDInst1` — Discharge Conveyor VSD (vsd-motor)

| REQ | Rel | Text | Class | Provenance | Open |
|---|---|---|---|---|---|
| REQ-082 | C1 | Runs on the plant automatic start command unless taken to hand, then on the hand start command | mode | ref VSD-01 | **Q-C04** |
| REQ-083 | C2 | Does not start while inhibited — `DiscreteInputs.AirStarDCIsoFB` | control | ref VSD-02 + io | — |
| REQ-084 | C3 | Does not start while faulted, nor while a fail-to-run fault stands | control | ref VSD-03 | — |
| REQ-085 | C4 | Does not start unless reported system-healthy | control | ref VSD-04 | **Q-C05** |
| REQ-086 | C5 | Does not start until the pre-start warning phase has completed | control | ref VSD-05 + io | — |
| REQ-087 | C6 | Runs at a speed setpoint (auto/hand), floored at a minimum speed, scaled against rated maximum | control | ref VSD-06 | **Q-C14** |
| REQ-088 | C7 | Can run in reverse; running confirmed from a forward or reverse running feedback | control | ref VSD-07 | **Q-C29** |
| REQ-089 | C8 | Declares itself enabled after a configurable up-to-speed time | control | ref VSD-08 | — |
| REQ-090 | C9 | On controlled shutdown, stops and declares shutdown complete after the shutdown time with both feedbacks cleared | control | ref VSD-09 | — |
| REQ-091 | C10 | Also declares shutdown complete immediately if faulted | control | ref VSD-10 | — |
| REQ-092 | C11 | Run-confirmation supervision not armed until the start-up allowance elapses; re-armed on direction change | control | ref VSD-11 + B-28 | — |
| REQ-093 | C12 | Fail-to-run fault raised and latched after the start-up allowance | control | ref VSD-12 | — |
| REQ-094 | C13 | Fail-to-stop fault raised and latched | control | ref VSD-13 | — |
| REQ-095 | C14 | A drive error reported by the VSD raises a latched fault | control | ref VSD-14 | **Q-C30** |
| REQ-096 | C15 | Fault reset clears all latched faults — `HMIControlSignals.SystemReset` | HMI | ref VSD-15 + io | **Q-C17** |
| REQ-097 | C16 | The drive's own condition is brought back for the operator (ready, warning, speeds, temperatures, current…) | HMI | ref VSD-16 | **Q-C30** |
| REQ-098 | C17 | Motion-sensor input used: running-forward confirmed from `DiscreteInputs.AirStarDCRotSen`, operator-bypassable via `HMIControlSignals.BypassAirStarDCRotSen` | control | ref VSD-17 + io + B-37/38 | **Q-C08** |
| REQ-099 | C18 | Running hours totalised | HMI | ref VSD-18 | — |
| REQ-100 | C19 | Operator status indication | HMI | ref VSD-19 | — |
| REQ-101 | C20 | Alarm indication for fail-to-run, fail-to-stop, drive error | alarm | ref VSD-20 | out of control-spec scope |
| REQ-102 | P1 | Start permitted only while the Overband Magnet is enabled | control | A + B-08 | — |
| REQ-103 | P2 | Start permitted only while the Equipment Control System is enabled | control | A + B-08 | — |
| REQ-104 | P3 | Hold through shutdown until the Air-Separator VSD reports shutdown complete | control | A + B-12 | — |
| REQ-105 | P4 | Start permitted only while the plant is in its running state | control | A + B-02 + io | — |
| REQ-106 | P5 | Start permitted only after the pre-start warning phase has completed | control | A + B-05 + io | — |
| REQ-107 | P6 | Hand intervention suppresses the automatic controlled-shutdown request | mode | A + B-19 | — |
| REQ-108 | P7 | Latched faults cleared by the plant-wide operator reset | HMI | A + B-24 + io | — |
| REQ-109 | P8 | This machine's enable is a start permissive for the Air-Separator VSD on **both** of its start paths | control | A + B-44 | — |
| REQ-110 | P9 | Inhibited while the local isolator feedback indicates isolation — `DiscreteInputs.AirStarDCIsoFB` | control | A + B-39 + io | — |
| REQ-111 | P10 | The run command is delivered through the drive interface, not a discrete start output (no candidate output exists) | control | B-40 + io-absence | — |

### `MotorVSDInst3` — Sorter Conveyor VSD (vsd-motor)

| REQ | Rel | Text | Class | Provenance | Open |
|---|---|---|---|---|---|
| REQ-112 | C1 | Runs on the plant automatic start command unless taken to hand, then on the hand start command | mode | ref VSD-01 | **Q-C04** |
| REQ-113 | C2 | Does not start while inhibited — `DiscreteInputs.OSCIsoFB` | control | ref VSD-02 + io | — |
| REQ-114 | C3 | Does not start while faulted, nor while a fail-to-run fault stands | control | ref VSD-03 | — |
| REQ-115 | C4 | Does not start unless reported system-healthy | control | ref VSD-04 | **Q-C05** |
| REQ-116 | C5 | Does not start until the pre-start warning phase has completed | control | ref VSD-05 + io | — |
| REQ-117 | C6 | Runs at a speed setpoint (auto/hand), floored at a minimum speed, scaled against rated maximum | control | ref VSD-06 | **Q-C14** |
| REQ-118 | C7 | Can run in reverse; running confirmed from a forward or reverse running feedback | control | ref VSD-07 | **Q-C33** |
| REQ-119 | C8 | Declares itself enabled after a configurable up-to-speed time | control | ref VSD-08 | — |
| REQ-120 | C9 | On controlled shutdown, stops and declares shutdown complete after the shutdown time | control | ref VSD-09 | — |
| REQ-121 | C10 | Also declares shutdown complete immediately if faulted | control | ref VSD-10 | — |
| REQ-122 | C11 | Supervision not armed until the start-up allowance elapses; re-armed on direction change | control | ref VSD-11 + B-28 | — |
| REQ-123 | C12 | Fail-to-run fault raised and latched | control | ref VSD-12 | — |
| REQ-124 | C13 | Fail-to-stop fault raised and latched | control | ref VSD-13 | — |
| REQ-125 | C14 | A drive error reported by the VSD raises a latched fault | control | ref VSD-14 | **Q-C30** |
| REQ-126 | C15 | Fault reset clears all latched faults — `HMIControlSignals.SystemReset` | HMI | ref VSD-15 + io | **Q-C17** |
| REQ-127 | C16 | The drive's own condition is brought back for the operator | HMI | ref VSD-16 | **Q-C30** |
| REQ-128 | C17 | Motion-sensor input: `DiscreteInputs.OSCRotSen` exists and **no source requirement uses it**; the per-conveyor bypass does not exist | control | ref VSD-17 + §8 sweep | **Q-C09** |
| REQ-129 | C18 | Running hours totalised | HMI | ref VSD-18 | — |
| REQ-130 | C19 | Operator status indication | HMI | ref VSD-19 | — |
| REQ-131 | C20 | Alarm indication for fail-to-run, fail-to-stop, drive error | alarm | ref VSD-20 | out of control-spec scope |
| REQ-132 | P1 | Start permitted only while the Optical Sorter reports itself ready — `DiscreteInputs.TomraReady` | control | A + B-11 + io | — |
| REQ-133 | P2 | Start permitted only while the Optical Sorter is free of faults | control | A + B-11 + io | **Q-C34** |
| REQ-134 | P3 | Whether the Optical Sorter's *enable* is ALSO required — **NOT ESTABLISHED** | control | A Q-A15 | **Q-C35** |
| REQ-135 | P4 | Start permitted only while the Ejected Material Conveyor is enabled | control | A + B-08 | — |
| REQ-136 | P5 | Start permitted only while the Residual Material Conveyor is enabled | control | A + B-08 | — |
| REQ-137 | P6 | Hold through shutdown until the Equipment Control System reports shutdown complete | control | A + B-12 | — |
| REQ-138 | P7 | Start permitted only while the plant is in its running state | control | A + B-02 + io | — |
| REQ-139 | P8 | Start permitted only after the pre-start warning phase has completed | control | A + B-05 + io | — |
| REQ-140 | P9 | Automatic controlled-shutdown suppressed under hand intervention — **whose hand is NOT ESTABLISHED** | mode | A delta + B-19/B-21 | **Q-C13** |
| REQ-141 | P10 | Latched faults cleared by the plant-wide operator reset | HMI | A + B-24 + io | — |
| REQ-142 | P11 | This machine's enable is the start permissive for Dust Filter Units 1 and 2 | control | A + B-08 | — |
| REQ-143 | P12 | This machine's enable is the start permissive for the Equipment Control System | control | A + B-08 | — |
| REQ-144 | P13 | The run command is delivered through the drive interface, not a discrete start output | control | B-40 + io-absence | — |
| REQ-145 | P14 | Inhibited while the local isolator feedback indicates isolation — `DiscreteInputs.OSCIsoFB` | control | A + B-39 + io | — |

### `TomraControlInst1` — Optical Sorter (optical-sorter)

| REQ | Rel | Text | Class | Provenance | Open |
|---|---|---|---|---|---|
| REQ-146 | C1 | Runs on the plant automatic start command unless taken to hand, then on the hand start command | mode | ref OS-01 | **Q-C04**, Q-A17 |
| REQ-147 | C2 | Does not start while inhibited | control | ref OS-02 | **Q-C11** |
| REQ-148 | C3 | Does not start while faulted, nor while a fail-to-run fault stands | control | ref OS-03 | — |
| REQ-149 | C4 | Does not start until the pre-start warning phase has completed (this class has **no** health permissive) | control | ref OS-04 + io | — |
| REQ-150 | C5 | Commanded both over the data link and by a hardwired run signal, from one run decision — `DiscreteOutputs.TomraRun` | control | ref OS-05 + io | **Q-C10** |
| REQ-151 | C6 | Condition read over the link and from hardwired ready/running/comms-fault — `DiscreteInputs.TomraReady/TomraRunning/TomraComFlt` | control | ref OS-06 + io | **Q-C10** |
| REQ-152 | C7 | Link liveness supervised; loss for a fixed watchdog period latches a communications-lost condition until reset | control | ref OS-07 | **Q-C10**, Q-B11 |
| REQ-153 | C8 | With a healthy link, running requires link **and** hardwired agreement; with a dead link the hardwired signal alone decides (same for comms fault) | control | ref OS-08 + io | — |
| REQ-154 | C9 | Declares itself enabled after a configurable up-to-speed time while reporting ready; withdraws the enable immediately on loss of running | control | ref OS-09 + io | — |
| REQ-155 | C10 | On controlled shutdown, stops and declares shutdown complete after the shutdown time (no fault/hand escape on this class) | control | ref OS-10 | Q-B06 |
| REQ-156 | C11 | Fail-to-run fault raised and latched | control | ref OS-11 | — |
| REQ-157 | C12 | Fail-to-stop fault raised and latched | control | ref OS-12 | — |
| REQ-158 | C13 | The sorter's own reported machine faults, plus loss of the link, raise a latched fault | control | ref OS-13 | **Q-C10** |
| REQ-159 | C14 | Fault reset clears all latched faults and is passed to the sorter over the link — `HMIControlSignals.SystemReset` | HMI | ref OS-14 + io | **Q-C10** |
| REQ-160 | C15 | Running hours totalised | HMI | ref OS-15 | — |
| REQ-161 | C16 | Operator status indication | HMI | ref OS-16 | — |
| REQ-162 | C17 | Wide alarm indication covering the sorter's fault set, fail-to-run, fail-to-stop, loss of link | alarm | ref OS-17 | out of control-spec scope |
| REQ-163 | C18 | Sorting program and belt on/off delays operator-settable | HMI | ref OS-18 | **Q-C36**, Q-A17 |
| REQ-164 | C19 | Byte order of the data exchanged with the sorter selectable at plant level | control | ref OS-19 | **Q-C18** |
| REQ-165 | P1 | Start permitted only while the Ejected Material Conveyor is enabled | control | A + B-08 | — |
| REQ-166 | P2 | Start permitted only while the Residual Material Conveyor is enabled | control | A + B-08 | — |
| REQ-167 | P3 | Hold through shutdown until the Sorter Conveyor VSD reports shutdown complete | control | A + B-12 | — |
| REQ-168 | P4 | Start permitted only while the plant is in its running state | control | A + B-02 + io | — |
| REQ-169 | P5 | Start permitted only after the pre-start warning phase has completed | control | A + B-05 + io | — |
| REQ-170 | P6 | Automatic controlled-shutdown suppressed under hand intervention — **whose hand is NOT ESTABLISHED** | mode | A delta + B-19/B-21 | **Q-C13** |
| REQ-171 | P7 | Latched faults cleared by the plant-wide operator reset | HMI | A + B-24 + io | — |
| REQ-172 | P8 | This machine's ready-and-not-faulted state is the start permissive for the Sorter Conveyor VSD | control | A + B-11 | Q-A15 |
| REQ-173 | P9 | The run command drives the physical run output — `DiscreteOutputs.TomraRun` | control | B-40 + io | — |
| REQ-174 | P10 | A faulted sorter does **not** release the machines held by its shutdown-complete | control | A O-2 + B-16 + ref | Q-A18 / Q-B06 |

## Open questions (rung C)

Full text lives in the specs and in `unclaimed-signals.md`; this is the index.

### Blocking

| Q | Instances | Summary |
|---|---|---|
| Q-C01 | `FilterUnitInst2` | `…Op` / `…Ready`: two requirements (running confirm, remote mode) competing for two same-shaped signals |
| Q-C02 | `FilterUnitInst3` | same competing pair on the sibling unit |
| Q-C03 | all three filter units | no isolator feedback exists although the class requires an inhibit permissive and 14 other machines have one |
| Q-C04 | all six | no per-machine hand/auto mode signal exists anywhere in the exported IO |
| Q-C05 | five (not the sorter) | system-healthy: a real plant health input exists unclaimed while the layer asserts health true |
| Q-C06 | `FilterUnitInst1` | `CycloneDustAutoRunning/Stop` — one signal, two asserted meanings; split-and-retain forbidden by the carve-out |
| Q-C08 | `MotorVSDInst1` | nothing confirms running while the motion-sensor bypass is active (candidate set empty) |
| Q-C09 | `MotorVSDInst3` | `OSCRotSen` exists and no requirement claims it; `BypassOSCRotSen` does not exist |
| Q-C10 | `TomraControlInst1` | the entire data-link word interface (`Tag_45`…`Tag_54`) is absent from the export |
| Q-C11 | `TomraControlInst1` | no isolator feedback |
| Q-C12 | all three filter units | how the two shutdown-hold conditions combine (both / either) |
| Q-C13 | `MotorVSDInst3`, `TomraControlInst1` | whose hand intervention suppresses whose shutdown |
| Q-C14 | both VSDs | speed setpoints: per-instance value vs plant-wide override, two mechanisms, no source |
| Q-C15 | plant | the eight global override flags and their seven values, ownerless |
| Q-C16 | filter units | `HandFansShutdown` — an unclaimed operator fan-shutdown command |
| Q-C17 | both VSDs | `VSDFaulrReset` — an unclaimed second, physical reset path |
| Q-C18 | plant, `TomraControlInst1` | test/simulation arrays wired into production control |
| Q-C19 | `FilterUnitInst1` | no start permissive established |
| Q-C20 | `FilterUnitInst1` | its enable serves no machine while its enable timer is configured |
| Q-C24 | plant | the whole anti-condensation feature set is unspecified |
| Q-C25 | plant | `GlobalSetAllToAuto` unclaimed |
| Q-C27 | `FilterUnitInst1` | system-healthy has three candidates, one already claimed by the fault requirement |
| Q-C29 | `MotorVSDInst1` | no reverse running feedback exists |
| Q-C30 | both VSDs | no drive error/condition signals exist in the exported IO |
| Q-C31 | plant | plant health inputs exist unclaimed while health is asserted true |
| Q-C33 | `MotorVSDInst3` | no running feedback of any kind exists for this machine |
| Q-C34 | `MotorVSDInst3` | "sorter not faulted": raw comms input vs aggregated FB fault output |
| Q-C35 | `MotorVSDInst3` | whether the sorter's *enable* is additionally required |
| Q-C36 | `TomraControlInst1` | no operator signals exist for program selection or belt delays |
| Q-C37 | plant | ownerless interlock members implying control (`SystemInterlock`=true, `PartInHand`, `UpStreamEnable`) |

### Non-blocking

Q-C23 (dust units' fault times differ: 2.0 s vs 3.0 s) · Q-C26 (`FilterUnitInst3` starts faulted in
the given boundary, sibling does not) · Q-C28 (cyclone fault time 60 s vs the dust units' 2–3 s) ·
Q-C32 (the `OSC*` prefix binds to `MotorVSDInst3` on instance-DB naming alone).

### Carried from upstream rungs, still blocking

Q-A01, Q-A02, Q-A03, Q-A04, Q-A05, Q-A06, Q-A10, Q-A11, Q-A13, Q-A16 · Q-B01, Q-B07, Q-B09, Q-B12,
Q-B13, Q-B17, Q-B18.

**Register status: BLOCKED.** 174 relations recorded, set-difference 0 both ways. **30 blocking
rung-C questions** (counted over the table above) plus 17 carried from rungs A and B. No relation was resolved to keep moving, and
no signal was invented.
