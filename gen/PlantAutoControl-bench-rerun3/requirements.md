# PlantAutoControl-bench-rerun3 — Requirements Register (derived view)

The register view of the per-equipment control specs in
`gen/PlantAutoControl-bench-rerun3/equipment-specs/`. **Derived, not authored**: every `REQ-nnn` below is
exactly one `C-nn` or `P-nn` relation from one spec, carrying that relation's id, instance,
provenance and open questions. This exists so `review-functional` has a trace target — it STOPs
without a register, so without this view a project specified through rungs A–D cannot be functionally
reviewed at all.

## Provenance

- **Produced:** 2026-08-05, `gen-equipment-spec` (rung C output section).
- **Derived from:** the six spec files in `gen/PlantAutoControl-bench-rerun3/equipment-specs/`.
- **Upstream:** rung A `equipment-topology.md`; rung B `plant-behaviours.md`; IO table
  `ir/PlantAutoControl-bench/{Input,Output,HMIControlSignals,PLC,Control,Timings}.ir`; class references
  `references/{FilterUnitSystem,vsd-motor,optical-sorter}/reference.md`.

### Completeness by set-difference

| Count | Value |
|---|---|
| `C-nn` relations across all six specs | **117** |
| `P-nn` relations across all six specs | **51** |
| **Union of spec relation ids** | **168** |
| **REQ ids in this register** | **168** |
| **Set difference (specs − register)** | **empty** |
| **Set difference (register − specs)** | **empty** |

Per-instance: `FilterUnitInst2` C18/P7 · `FilterUnitInst3` C18/P7 · `FilterUnitInst1` C18/P6 ·
`MotorVSDInst1` C22/P9 · `MotorVSDInst3` C22/P12 · `TomraControlInst1` C19/P10.
Counts machine-checked with `grep -cE '^- \*\*C[0-9]+\*\*'` / `'^- \*\*P[0-9]+\*\*'` per file, not
tallied by eye.

### Reading rules

- **IDs are stable forever** within this run's namespace. `Source` is always `<Instance> <relation>`.
- **Class:** `control` · `mode` · `HMI` · `timing` · `out-of-scope`.
- **Tag status:** every tag named in a spec is `exists` (grep/`tagstatus`-verified), `MEMBER-NOT-FOUND`
  (root real, member absent) or `proposed`. **Nothing in this run is coded against a proposed tag.**
- **`Q` column:** the blocking/non-blocking questions attached to that relation. A REQ carrying a
  **BLOCKING** Q is **not buildable** as it stands. **65 of the 168 relations carry at least one
  blocking question.**
- **Inherited-but-unimplementable relations are present, not omitted.** Where a class requirement has
  no signal (isolation permissive, system-healthy permissive), the REQ exists and carries its Q. A
  register that quietly dropped them would let a reviewer's reverse pass report a correctly-rendered
  term as unrequested logic.

---

### `FilterUnitInst2` — Dust Filter Unit 1 (FilterUnitSystem)

| REQ | relation | Text | Class | Q |
|---|---|---|---|---|
| REQ-001 | C1 | Runs on the plant automatic start command, or on the operator's hand start command while in hand | control | — |
| REQ-002 | C2 | Does not start while inhibited (isolated / locked off) | control | **Q-C03** |
| REQ-003 | C3 | Does not start while it has an active fault | control | — |
| REQ-004 | C4 | Does not start unless reported system-healthy | control | **Q-C20** |
| REQ-005 | C5 | Does not start unless in remote operating mode | mode | **Q-C01** |
| REQ-006 | C6 | Does not start until the plant pre-start warning phase has completed | control | — |
| REQ-007 | C7 | Declares itself enabled after running for the fan up-to-speed time | control | — |
| REQ-008 | C8 | On controlled shutdown stops, declaring shutdown complete after the shutdown time with running feedback cleared | control | — |
| REQ-009 | C9 | Declares shutdown complete immediately if faulted, or in hand and not called to run | control | — |
| REQ-010 | C10 | Commanded but not running within the fault time raises a latched fail-to-run fault | control | — |
| REQ-011 | C11 | Running while not commanded for the fault time raises a latched fail-to-stop fault | control | — |
| REQ-012 | C12 | A unit fault feedback raises a latched fault (`DiscreteInputs.FilterUnit1Flt`) | control | — |
| REQ-013 | C13 | Faults cleared by the single plant-wide operator reset, echoed to `DiscreteOutputs.FilterUnit1Reset` | HMI | — |
| REQ-014 | C14 | Running state taken from its own running feedback; no motion sensor on this class | control | **Q-C01** |
| REQ-015 | C15 | Running hours totalised | control | — |
| REQ-016 | C16 | Operator status indication published | HMI | — |
| REQ-017 | C17 | Alarm indication (fail-to-run / fail-to-stop / unit fault / not-in-remote) | out-of-scope | alarms are a separate artifact |
| REQ-018 | C18 | Run command drives `DiscreteOutputs.FilterUnit1Start` | control | — |
| REQ-019 | P1 | Start permitted only while `MotorVSDInst3` is enabled | control | — |
| REQ-020 | P2 | On controlled shutdown, hold until `PlantControl.FansShutdownReady` | control | **Q-C09** |
| REQ-021 | P3 | On controlled shutdown, hold until `AirStarInst1` reports shutdown complete | control | **Q-C09** |
| REQ-022 | P4 | This unit's enable is a permissive for `AirStarInst1`'s start | control | — |
| REQ-023 | P5 | `AirStarInst1` has a second start path not requiring this unit's enable | control | — |
| REQ-024 | P6 | Commanded to start and run automatically while the plant is in its running state | control | — |
| REQ-025 | P7 | Automatic controlled-shutdown request suppressed while under hand intervention | mode | — |

### `FilterUnitInst3` — Dust Filter Unit 2 (FilterUnitSystem)

| REQ | relation | Text | Class | Q |
|---|---|---|---|---|
| REQ-026 | C1 | Runs on the plant automatic start command, or on the operator's hand start command while in hand | control | — |
| REQ-027 | C2 | Does not start while inhibited (isolated / locked off) | control | **Q-C03** |
| REQ-028 | C3 | Does not start while it has an active fault | control | — |
| REQ-029 | C4 | Does not start unless reported system-healthy | control | **Q-C20** |
| REQ-030 | C5 | Does not start unless in remote operating mode | mode | **Q-C02** |
| REQ-031 | C6 | Does not start until the plant pre-start warning phase has completed | control | — |
| REQ-032 | C7 | Declares itself enabled after running for the fan up-to-speed time | control | — |
| REQ-033 | C8 | On controlled shutdown stops, declaring shutdown complete after the shutdown time with running feedback cleared | control | — |
| REQ-034 | C9 | Declares shutdown complete immediately if faulted, or in hand and not called to run | control | — |
| REQ-035 | C10 | Commanded but not running within the fault time raises a latched fail-to-run fault | control | — |
| REQ-036 | C11 | Running while not commanded for the fault time raises a latched fail-to-stop fault | control | — |
| REQ-037 | C12 | A unit fault feedback raises a latched fault (`DiscreteInputs.FilterUnit2Flt`) | control | — |
| REQ-038 | C13 | Faults cleared by the plant-wide operator reset, echoed to `DiscreteOutputs.FilterUnit2Reset` | HMI | — |
| REQ-039 | C14 | Running state taken from its own running feedback; no motion sensor on this class | control | **Q-C02** |
| REQ-040 | C15 | Running hours totalised | control | — |
| REQ-041 | C16 | Operator status indication published | HMI | — |
| REQ-042 | C17 | Alarm indication | out-of-scope | alarms separate |
| REQ-043 | C18 | Run command drives `DiscreteOutputs.FilterUnit2Start` | control | — |
| REQ-044 | P1 | Start permitted only while `MotorVSDInst3` is enabled | control | — |
| REQ-045 | P2 | On controlled shutdown, hold until `PlantControl.FansShutdownReady` | control | **Q-C09** |
| REQ-046 | P3 | On controlled shutdown, hold until `AirStarInst1` reports shutdown complete | control | **Q-C09** |
| REQ-047 | P4 | This unit's enable is a permissive for `AirStarInst1`'s start | control | — |
| REQ-048 | P5 | `AirStarInst1` has a second start path not requiring this unit's enable | control | — |
| REQ-049 | P6 | Commanded to start and run automatically while the plant is in its running state | control | — |
| REQ-050 | P7 | Automatic controlled-shutdown request suppressed while under hand intervention | mode | — |

### `FilterUnitInst1` — Cyclone Filter Unit (FilterUnitSystem)

| REQ | relation | Text | Class | Q |
|---|---|---|---|---|
| REQ-051 | C1 | Runs on the plant automatic start command, or on the operator's hand start command while in hand | control | — |
| REQ-052 | C2 | Does not start while inhibited (isolated / locked off) | control | **Q-C03** |
| REQ-053 | C3 | Does not start while it has an active fault | control | — |
| REQ-054 | C4 | Does not start unless reported system-healthy | control | **Q-C20** |
| REQ-055 | C5 | Does not start unless in remote operating mode (`DiscreteInputs.CycloneDustRemOp`) | mode | — |
| REQ-056 | C6 | Does not start until the plant pre-start warning phase has completed | control | — |
| REQ-057 | C7 | Declares itself enabled after running for the fan up-to-speed time — **this enable permits no machine** | control | Q-A7 |
| REQ-058 | C8 | On controlled shutdown stops, declaring shutdown complete after the shutdown time with running feedback cleared | control | — |
| REQ-059 | C9 | Declares shutdown complete immediately if faulted, or in hand and not called to run | control | — |
| REQ-060 | C10 | Commanded but not running within the fault time raises a latched fail-to-run fault | control | — |
| REQ-061 | C11 | Running while not commanded for the fault time raises a latched fail-to-stop fault | control | — |
| REQ-062 | C12 | Fault condition carried by `DiscreteInputs.CycloneDustSysOk` in its healthy sense (no direct fault input) | control | — |
| REQ-063 | C13 | Faults cleared by the plant-wide operator reset, echoed to `DiscreteOutputs.CycloneDustFilterReset` | HMI | — |
| REQ-064 | C14 | Running state taken from `DiscreteInputs.CycloneDustAutoRunning/Stop` | control | **Q-C05** |
| REQ-065 | C15 | Running hours totalised | control | — |
| REQ-066 | C16 | Operator status indication published | HMI | — |
| REQ-067 | C17 | Alarm indication | out-of-scope | alarms separate |
| REQ-068 | C18 | Run command drives `DiscreteOutputs.CycloneDustFilterStart` | control | — |
| REQ-069 | P1 | No neighbour start permissive — starts on the plant run state alone | control | — |
| REQ-070 | P2 | On controlled shutdown, hold until `PlantControl.FansShutdownReady` | control | **Q-C09** |
| REQ-071 | P3 | On controlled shutdown, hold until `ShredderControlInst1` reports shutdown complete | control | **Q-C09** |
| REQ-072 | P4 | This unit's enable permits no machine | control | Q-A7 |
| REQ-073 | P5 | Commanded to start and run automatically while the plant is in its running state | control | — |
| REQ-074 | P6 | Automatic controlled-shutdown request suppressed while under hand intervention | mode | — |

### `MotorVSDInst1` — Discharge Conveyor VSD (vsd-motor)

| REQ | relation | Text | Class | Q |
|---|---|---|---|---|
| REQ-075 | C1 | Runs on the plant automatic start command, or on the operator's hand start command while in hand | control | — |
| REQ-076 | C2 | Does not start while inhibited (`DiscreteInputs.AirStarDCIsoFB`) | control | — |
| REQ-077 | C3 | Does not start while faulted, or while a fail-to-run fault is standing | control | — |
| REQ-078 | C4 | Does not start unless reported system-healthy | control | **Q-C20** |
| REQ-079 | C5 | Does not start until the plant pre-start warning phase has completed | control | — |
| REQ-080 | C6 | Runs at a speed setpoint (auto / hand), floored at a minimum speed, scaled to rated maximum | control | Q-C15 |
| REQ-081 | C7 | Can be commanded in reverse; running confirmed from a forward or reverse feedback | control | **Q-C16** |
| REQ-082 | C8 | Declares itself enabled after running for the up-to-speed time | control | — |
| REQ-083 | C9 | On controlled shutdown stops, declaring shutdown complete after the shutdown time with both running feedbacks cleared | control | — |
| REQ-084 | C10 | Declares shutdown complete immediately if faulted | control | — |
| REQ-085 | C11 | Running-forward confirmation taken from `DiscreteInputs.AirStarDCRotSen`; sensor not used while `HMIControlSignals.BypassAirStarDCRotSen` is set | control | Q-C18 |
| REQ-086 | C12 | Run-confirmation supervision armed only after the start-up allowance, re-armed on direction change | control | — |
| REQ-087 | C13 | Commanded but not running within the fault time raises a latched fail-to-run fault | control | — |
| REQ-088 | C14 | Running while not commanded for the fault time raises a latched fail-to-stop fault | control | — |
| REQ-089 | C15 | A drive error raises a latched fault (interface-carried, no plant IO) | control | Q-C17 |
| REQ-090 | C16 | Faults cleared by the plant-wide operator reset | HMI | Q-C21 |
| REQ-091 | C17 | Drive condition set published for the operator (interface-carried) | HMI | Q-C17 |
| REQ-092 | C18 | This instance uses its motion sensor (class declares the input but never consumes it) | control | — |
| REQ-093 | C19 | Running hours totalised | control | — |
| REQ-094 | C20 | Operator status indication published | HMI | — |
| REQ-095 | C21 | Alarm indication | out-of-scope | alarms separate |
| REQ-096 | C22 | The run command reaches the drive — **command path unspecified, no discrete start output exists** | control | **Q-C08** |
| REQ-097 | P1 | Start permitted only while `MotorStarterInst8` (Overband Magnet) is enabled | control | — |
| REQ-098 | P2 | Start permitted only while `ECSControlInst1` is enabled | control | — |
| REQ-099 | P3 | On controlled shutdown, hold until `AirStarInst1` reports shutdown complete | control | — |
| REQ-100 | P4 | This machine's enable is a permissive on both of `AirStarInst1`'s start paths | control | — |
| REQ-101 | P5 | `MotorStarterInst8` holds its shutdown until this machine reports shutdown complete | control | — |
| REQ-102 | P6 | `ECSControlInst1` holds its shutdown until this machine reports shutdown complete | control | — |
| REQ-103 | P7 | Commanded to start and run automatically while the plant is in its running state | control | — |
| REQ-104 | P8 | Automatic controlled-shutdown request suppressed while under hand intervention | mode | — |
| REQ-105 | P9 | **DISPUTED** — start gated by `PlantControl.GeneralEnable`, or no sub-system gate at all | mode | **Q-C11** |

### `MotorVSDInst3` — Sorter Conveyor VSD (vsd-motor)

| REQ | relation | Text | Class | Q |
|---|---|---|---|---|
| REQ-106 | C1 | Runs on the plant automatic start command, or on the operator's hand start command while in hand | control | — |
| REQ-107 | C2 | Does not start while inhibited (`DiscreteInputs.OSCIsoFB`) | control | — |
| REQ-108 | C3 | Does not start while faulted, or while a fail-to-run fault is standing | control | — |
| REQ-109 | C4 | Does not start unless reported system-healthy | control | **Q-C20** |
| REQ-110 | C5 | Does not start until the plant pre-start warning phase has completed | control | — |
| REQ-111 | C6 | Runs at a speed setpoint (auto / hand), floored at a minimum speed, scaled to rated maximum | control | Q-C15 |
| REQ-112 | C7 | Can be commanded in reverse; running confirmed from a forward or reverse feedback | control | **Q-C16** |
| REQ-113 | C8 | Declares itself enabled after running for the up-to-speed time — consumed by three machines | control | — |
| REQ-114 | C9 | On controlled shutdown stops, declaring shutdown complete after the shutdown time with both running feedbacks cleared | control | — |
| REQ-115 | C10 | Declares shutdown complete immediately if faulted | control | — |
| REQ-116 | C11 | Running confirmation taken from a running feedback — **no bindable source exists** | control | **Q-C06** |
| REQ-117 | C12 | Run-confirmation supervision armed only after the start-up allowance, re-armed on direction change | control | — |
| REQ-118 | C13 | Commanded but not running within the fault time raises a latched fail-to-run fault | control | — |
| REQ-119 | C14 | Running while not commanded for the fault time raises a latched fail-to-stop fault | control | — |
| REQ-120 | C15 | A drive error raises a latched fault (interface-carried) | control | Q-C17 |
| REQ-121 | C16 | Faults cleared by the plant-wide operator reset | HMI | Q-C21 |
| REQ-122 | C17 | Drive condition set published for the operator (interface-carried) | HMI | Q-C17 |
| REQ-123 | C18 | A motion sensor exists (`DiscreteInputs.OSCRotSen`) with **no stated use and no operator bypass** | control | **Q-C07** |
| REQ-124 | C19 | Running hours totalised | control | — |
| REQ-125 | C20 | Operator status indication published | HMI | — |
| REQ-126 | C21 | Alarm indication | out-of-scope | alarms separate |
| REQ-127 | C22 | Run command reaches the drive through the drive interface, not a discrete output | control | — |
| REQ-128 | P1 | Start permitted only while `TomraControlInst1` reports itself ready | control | **Q-C12** |
| REQ-129 | P2 | Start permitted only while `TomraControlInst1` is not faulted | control | **Q-C12** |
| REQ-130 | P3 | Start permitted only while `MotorStarterInst6` (Ejected Material Conveyor) is enabled | control | — |
| REQ-131 | P4 | Start permitted only while `MotorStarterInst7` (Residual Material Conveyor) is enabled | control | — |
| REQ-132 | P5 | On controlled shutdown, hold until `ECSControlInst1` reports shutdown complete | control | — |
| REQ-133 | P6 | `TomraControlInst1` holds its shutdown until this machine reports shutdown complete | control | — |
| REQ-134 | P7 | This machine's enable permits `FilterUnitInst2` to start | control | — |
| REQ-135 | P8 | This machine's enable permits `FilterUnitInst3` to start | control | — |
| REQ-136 | P9 | This machine's enable permits `ECSControlInst1` to start | control | — |
| REQ-137 | P10 | Commanded to start and run automatically while the plant is in its running state | control | — |
| REQ-138 | P11 | **DISPUTED** — auto-shutdown suppression gated on this machine's own hand state, or on another machine's | mode | **Q-C10** |
| REQ-139 | P12 | REQ-130/131 have no reciprocal shutdown hold — those conveyors release on the sorter's shutdown-complete | control | — |

### `TomraControlInst1` — Optical Sorter (optical-sorter)

| REQ | relation | Text | Class | Q |
|---|---|---|---|---|
| REQ-140 | C1 | Runs on the plant automatic start command, or on the operator's hand start command while in hand | control | — |
| REQ-141 | C2 | Does not start while inhibited (isolated / locked off) | control | **Q-C03** |
| REQ-142 | C3 | Does not start while faulted, or while a fail-to-run fault is standing | control | — |
| REQ-143 | C4 | Does not start until the plant pre-start warning phase has completed | control | — |
| REQ-144 | C5 | Commanded over the data link and by `DiscreteOutputs.TomraRun`, both from the same run decision | control | **Q-C14** |
| REQ-145 | C6 | Condition read over the link and from `TomraReady` / `TomraRunning` / `TomraComFlt` | control | — |
| REQ-146 | C7 | Data link supervised by a liveness indication; loss for the watchdog period latches comms-lost | control | **Q-C14** |
| REQ-147 | C8 | Counts as running on link + hardwired while the link is healthy; hardwired alone while dead; same fallback for the comms-fault indication | control | — |
| REQ-148 | C9 | Declares itself enabled after running for the up-to-speed time while reporting ready; withdraws it immediately on loss of running | control | Q-A6 |
| REQ-149 | C10 | On controlled shutdown stops, declaring shutdown complete after the shutdown time once stopped | control | — |
| REQ-150 | C11 | Commanded but not running within the fault time raises a latched fail-to-run fault | control | — |
| REQ-151 | C12 | Running while not commanded for the fault time raises a latched fail-to-stop fault | control | — |
| REQ-152 | C13 | The sorter's own reported faults plus loss of the data link raise a latched fault | control | **Q-C14** |
| REQ-153 | C14 | Faults cleared by the plant-wide operator reset and passed to the sorter over the link | HMI | **Q-C14** |
| REQ-154 | C15 | Running hours totalised | control | — |
| REQ-155 | C16 | Operator status indication published | HMI | — |
| REQ-156 | C17 | Wide alarm indication | out-of-scope | alarms separate |
| REQ-157 | C18 | Sorting program and belt on/off delays operator-settable — **an interface promise the as-built does not implement** | HMI | Q-C19 |
| REQ-158 | C19 | Data byte order selectable at plant level — **carried on `PlantControl.Test[5]`, a test/simulation array** | control | **Q-C13** |
| REQ-159 | P1 | Start permitted only while `MotorStarterInst6` is enabled | control | — |
| REQ-160 | P2 | Start permitted only while `MotorStarterInst7` is enabled | control | — |
| REQ-161 | P3 | On controlled shutdown, hold until `MotorVSDInst3` reports shutdown complete | control | — |
| REQ-162 | P4 | `MotorStarterInst6` holds its shutdown until this machine reports shutdown complete | control | — |
| REQ-163 | P5 | `MotorStarterInst7` holds its shutdown until this machine reports shutdown complete | control | — |
| REQ-164 | P6 | This machine's ready state is a permissive for `MotorVSDInst3`'s start | control | **Q-C12** |
| REQ-165 | P7 | This machine's not-faulted state is a permissive for `MotorVSDInst3`'s start | control | **Q-C12** |
| REQ-166 | P8 | Commanded to start and run automatically while the plant is in its running state | control | — |
| REQ-167 | P9 | **DISPUTED** — auto-shutdown suppression gated on this machine's own hand state, or on another machine's | mode | **Q-C10** |
| REQ-168 | P10 | Shutdown-complete has no fault escape, so a fault here does not release REQ-162/163 | control | Q-A5 |

---

## Open questions (rung C), and the questions carried into it

**BLOCKING (16 at rung C, plus 5 carried from A and B):**

| Q | What it blocks | Instances |
|---|---|---|
| Q-C01 | running vs remote-operational binding, `FilterUnit1Ready`/`FilterUnit1Op` | FilterUnitInst2 |
| Q-C02 | same for `FilterUnit2Ready`/`FilterUnit2Op` | FilterUnitInst3 |
| Q-C03 | isolation permissive with no signal (MEMBER-NOT-FOUND) | FilterUnitInst1/2/3, TomraControlInst1 |
| Q-C04 | `HMIControlSignals.HandFansShutdown` claimed by nothing | FilterUnitInst1/2/3 |
| Q-C05 | what `CycloneDustAutoRunning/Stop` asserts | FilterUnitInst1 |
| Q-C06 | no bindable running confirmation | MotorVSDInst3 |
| Q-C07 | motion sensor unclaimed, and uniquely without an operator bypass | MotorVSDInst3 |
| Q-C08 | no command path for the run command | MotorVSDInst1 |
| Q-C09 | how the two shutdown-hold conditions combine (carries Q-A1 / Q-B7) | FilterUnitInst1/2/3 |
| Q-C10 | whose hand state suppresses auto shutdown (carries Q-B3) | MotorVSDInst3, TomraControlInst1 |
| Q-C11 | does the general enable gate this machine (carries Q-B2) | MotorVSDInst1 |
| Q-C12 | which ready / which fault signal satisfies the neighbour's permissive | MotorVSDInst3, TomraControlInst1 |
| Q-C13 | a production function on a test/simulation array element | TomraControlInst1 |
| Q-C14 | the sorter's data words are PROPOSED tags | TomraControlInst1 |
| Q-C20 | plant health / surge inputs owned by nobody; five C4 relations unsatisfied | all except TomraControlInst1 |
| Q-C21 | `DiscreteOutputs.VSDFaulrReset` claimed by nobody | MotorVSDInst1, MotorVSDInst3 |
| Q-C22 | anti-condensation: a whole unspecified feature over this equipment | plant |
| Q-C23 | a global settings-override mechanism over these machines' unstated settings | plant |
| Q-C24 | `GlobalSetAllToAuto`: a plant-wide mode command | plant |
| Q-C25 | six plant-wide interlock/mode flags with no stated function | plant |
| Q-C26 | three test/simulation arrays shadowing the real IO, one already in a production path | plant |

**Non-blocking:** Q-C15 (settings with no stated value), Q-C16 (reverse capability absent or
unsourced — raised blocking on both VSDs' C7 and repeated here), Q-C17 (drive error / condition set
interface-carried), Q-C18 (bypass leaves running confirmation sourceless), Q-C19 (unimplemented
operator settings).

**Carried from rung A:** Q-A1 (→ Q-C09), Q-A2 (references are code-derived, not standards — blocking
the completeness claim of every `C-nn` above), Q-A3 (single-instance class, `TomraControlInst1`
deltas not separable), Q-A4 (material flow not stated), Q-A5, Q-A6, Q-A7.

**Carried from rung B:** Q-B1 (the whole behaviour source is circular — blocking the claim that any
REQ here states what the plant *should* do), Q-B2 (→ Q-C11), Q-B3 (→ Q-C10), Q-B4 (→ Q-C03, now with
mechanical evidence that no isolation signal exists), Q-B5 (→ Q-C06/Q-C07), Q-B6 (→ Q-C15, sharpened
by Q-C23), Q-B7 (→ Q-C09), Q-B8 (**resolved on IO evidence**: no `AirStarDCStart`/`OSCStart` exists
anywhere in `DiscreteOutputs`, so neither VSD has a discrete start output — the residual question
about `MotorVSDInst1`'s actual command path is now Q-C08).
