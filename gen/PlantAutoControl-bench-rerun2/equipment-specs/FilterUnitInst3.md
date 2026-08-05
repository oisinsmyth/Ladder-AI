# EQUIPMENT SPEC — Dust Filter Unit 2 (`FilterUnitInst3`)

Rung C (`/gen-equipment-spec`), run `PlantAutoControl-bench-rerun2`, 2026-08-05.
**Signals are bound here. Booleans, interface members and network structure are NOT.**

```
class: FilterUnitSystem (ref v0-derived 2026-08-04)   FB type: FilterUnitSystem
fed-by:        NOT ESTABLISHED — no material-flow source (Q-A03)
discharges-to: NOT ESTABLISHED — no material-flow source (Q-A03)
serves:        Air-Separator VSD (enable-wise) [A]
```

**Tag verification method:** grep-verified member-by-member against `ir/PlantAutoControl-bench/*.ir`.
`converter tagstatus` reported 0 proposed but its verdict is **not** relied on — it validates only
the root DB name, not the member path.

**This instance is enumerated in full, not by reference to Dust Filter Unit 1**, although the two
are identical in every field the sources provide. Whether that identity is real is Q-A09; two
settings differ (see SETTINGS) and one initial state differs (Q-C26).

## IO BINDING [io]

| Role | Signal | Verified |
|---|---|---|
| run command → field | `DiscreteOutputs.FilterUnit2Start` | grep ✓ |
| fault reset → field | `DiscreteOutputs.FilterUnit2Reset` | grep ✓ |
| unit fault report | `DiscreteInputs.FilterUnit2Flt` | grep ✓ |
| running confirm / remote-operational | **UNRESOLVED** — `DiscreteInputs.FilterUnit2Op`, `DiscreteInputs.FilterUnit2Ready` | grep ✓ (both) |
| plant run state | `PlantControl.Status` | grep ✓ |
| plant pre-start complete | `PlantControl.PreStartComplete` | grep ✓ |
| plant fans-shutdown-ready | `PlantControl.FansShutdownReady` | grep ✓ |
| plant-wide operator reset | `HMIControlSignals.SystemReset` | grep ✓ |
| fan start-up time setting | `ProcessTimings.NormalFanStartTime` (= 10.0) | grep ✓ |
| inhibit / isolator | **NO SIGNAL EXISTS** — Q-C03 | — |
| hand/auto mode source | **NO SIGNAL EXISTS** — Q-C04 | — |
| system-healthy source | **UNRESOLVED** — Q-C05 | — |

## CONTROL REQUIREMENTS (complete by construction, FU-01…FU-17)

- **C1** the unit runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead `[ref FU-01]`
  **CANDIDATES:** `InterlockData.PartInHand` (plant-level, wrong granularity) ·
  `FilterUnitInst3.IO.InHand` `[iface, named-only]` · `FilterUnitInst3.IO.HandStartSignal`
  `[iface, named-only]` → **BLOCKING Q-C04**
- **C2** the unit does not start while it is inhibited (isolated / locked off) `[ref FU-02]`
  **CANDIDATES:** *(empty — no FilterUnitSystem isolator signal exists)* → **BLOCKING Q-C03**
- **C3** the unit does not start while it has an active fault `[ref FU-03 + io]` —
  `DiscreteInputs.FilterUnit2Flt`
- **C4** the unit does not start unless it is reported system-healthy `[ref FU-04]`
  **CANDIDATES:** `DiscreteInputs.ControlHealthy` · *a hard-asserted constant true per `[B-34]`*
  → **BLOCKING Q-C05**
- **C5** the unit does not start unless it is in remote (not local) operating mode `[ref FU-05]`
  **CANDIDATES:** `DiscreteInputs.FilterUnit2Ready` · `DiscreteInputs.FilterUnit2Op` ·
  `FilterUnitInst3.RemoteOp` `[iface, named-only]` → **BLOCKING Q-C02** (this instance's copy of the
  same competing pair; see C14)
- **C6** the unit does not start until the plant pre-start warning phase has completed
  `[ref FU-06 + io]` — `PlantControl.PreStartComplete`
- **C7** once running and confirmed running for a configurable up-to-speed time, the unit declares
  itself enabled, which permits the machine it serves to start `[ref FU-07 + io]` —
  `ProcessTimings.NormalFanStartTime` (10.0 s)
- **C8** on a controlled shutdown request the unit stops, and declares its shutdown complete once a
  configurable shutdown time has elapsed and its running feedback has cleared `[ref FU-08]`
- **C9** the unit also declares shutdown complete immediately if it is faulted, or if it is under
  hand control and the operator is not calling it to run `[ref FU-09]`
- **C10** a fail-to-run fault is raised and latched if the unit is commanded but does not report
  running within a configurable fault time `[ref FU-10]`
- **C11** a fail-to-stop fault is raised and latched if the unit reports running while not commanded
  for that same fault time `[ref FU-11]`
- **C12** a fault feedback from the unit itself raises a latched fault `[ref FU-12 + io]` —
  `DiscreteInputs.FilterUnit2Flt`
- **C13** a fault-reset command clears all latched faults `[ref FU-13 + io]` —
  `HMIControlSignals.SystemReset`
- **C14** the unit's actual running state is taken from its own running feedback; no motion sensor
  `[ref FU-14]`
  **CANDIDATES:** `DiscreteInputs.FilterUnit2Op` · `DiscreteInputs.FilterUnit2Ready`
  → **BLOCKING Q-C02** — two requirements (C5 and this) competing for two same-shaped signals.
- **C15** running hours are totalised `[ref FU-15]`
- **C16** a status indication is published for the operator `[ref FU-16]`
- **C17** an alarm indication is published for fail-to-run, fail-to-stop, unit fault feedback, and
  not-in-remote `[ref FU-17]` — *alarms out of scope for this control spec; recorded for
  completeness.*

## PLANT INTERLOCKS

- **P1** start permitted only while the Sorter Conveyor VSD (`MotorVSDInst3`) is enabled `[A + B-08]`
- **P2** on controlled shutdown, hold until the plant reports the filter fans ready to shut down
  `[A + B-17 + io]` — `PlantControl.FansShutdownReady`  **(Q-C12)**
- **P3** on controlled shutdown, hold until the Air-Separator VSD reports its shutdown complete
  `[A + B-17]`  **(Q-C12)**
  > Two lines by rule: both hold conditions, different signals, combination unsettled.
- **P4** start permitted only while the plant is in its running state `[A + B-02 + io]` —
  `PlantControl.Status`
- **P5** start permitted only after the plant pre-start warning phase has completed
  `[A + B-05 + io]` — `PlantControl.PreStartComplete`
- **P6** while this unit is under hand intervention, its automatic controlled-shutdown request is
  suppressed `[A + B-19]`
- **P7** this unit's latched faults are cleared by the single plant-wide operator reset
  `[A + B-24 + io]` — `HMIControlSignals.SystemReset`
- **P8** the plant-wide reset is additionally echoed to the unit as a physical reset command
  `[A delta + B-25 + io]` — `DiscreteOutputs.FilterUnit2Reset`
- **P9** this unit's enable is one of the start permissives for the Air-Separator VSD, on that
  machine's primary start path only `[A + B-44]`
- **P10** the unit's resulting run command drives its physical start output `[B-40 + io]` —
  `DiscreteOutputs.FilterUnit2Start`

## SETTINGS

| Setting | Owner | Value | Provenance |
|---|---|---|---|
| fan start-up (up-to-speed) time | engineering | **10.0 s** | `ProcessTimings.NormalFanStartTime` `[io]`, `[B-43]` |
| shutdown time | engineering | **5.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| fail-to-run / fail-to-stop fault time | engineering | **3.0 s** | given instance-DB start value `(boundary)`. **Differs from Dust Filter Unit 1's 2.0 s** for two units every source describes identically — **Q-C23**. |
| running-hours totaliser interval | engineering | 1 h | given instance-DB start value `(boundary)` |
| remote-operating-mode permissive | engineering | static setpoint, value not initialised | given instance-DB `(boundary)`; interacts with Q-C02 |

## UNCLAIMED IO (§8a per-instance sweep)

| Signal | Disposition |
|---|---|
| `DiscreteInputs.FilterUnit2Ready` | claimed but unresolved — C5/C14 candidate set (Q-C02) |
| `DiscreteInputs.FilterUnit2Op` | claimed but unresolved — C5/C14 candidate set (Q-C02) |
| `DiscreteInputs.FilterUnit2Flt` | bound — C3, C12 |
| `DiscreteOutputs.FilterUnit2Start` | bound — P10 |
| `DiscreteOutputs.FilterUnit2Reset` | bound — P8 |
| *(no `FilterUnit2IsoFB`)* | absent — Q-C03 |
| `HMIControlSignals.HandFansShutdown` | UNCLAIMED, control-implying — **Q-C16** (plant-level) |
| anti-condensation set (`AntiConEnabled`, `RunAntiConNxtStart`, `SkipAntiConNxtStart`, `AntiConRunRequired`, `AntiCondensationTime`) | UNCLAIMED, control-implying — **Q-C24** (plant-level) |
| global time-override set | UNCLAIMED, control-implying — **Q-C15** (plant-level) |
| `HMIControlSignals.GlobalSetAllToAuto` | UNCLAIMED, control-implying — **Q-C25** (plant-level) |

## DELTAS

- **Δ1 (+) on P8** — plant-wide reset echoed to the unit as a physical reset command.
- **Δ2 (?) on C5/C14** — an operational-availability report and a ready report from the field
  against one running feedback and one remote-mode permissive in the class. Q-C02.
- **Δ3 (−) on C2** — no isolator feedback although the class requires an inhibit permissive. Q-C03.
- **Δ4 (?) new at this rung** — this unit's given boundary starts with its fault latched and its
  operator status word at the faulted value, where the sibling unit starts clear. Whether that is a
  real commissioning state or an artefact of the snapshot is **Q-C26** (non-blocking).

**DELTAS: none-further-found** — searched: rung-A delta block for this instance, `FilterUnitSystem`
reference §"Class requirement set" and §"Class deltas", rung-B behaviours scoped to this instance,
the §8a sweep above, the §8b plant residual list, and a field-by-field comparison against
`FilterUnitInst2` (which produced Δ4 and Q-C23).

## OPEN

**Blocking:** Q-C02, Q-C03, Q-C04, Q-C05, Q-C12, Q-C15, Q-C16, Q-C24, Q-C25.
**Carried, still blocking:** Q-A01, Q-A03, Q-A04, Q-B01, Q-B07, Q-B12.
**Non-blocking:** Q-C23, Q-C26, Q-A08, Q-A09, Q-B21.
