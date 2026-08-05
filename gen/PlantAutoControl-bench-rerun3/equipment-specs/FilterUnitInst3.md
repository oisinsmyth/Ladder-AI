# EQUIPMENT SPEC — FilterUnitInst3 (Dust Filter Unit 2)

    class: FilterUnitSystem (reference v0-derived, 2026-08-04)
    fed-by: not-stated-by-source [A, Q-A4]      discharges-to: not-stated-by-source [A, Q-A4]
    serves: Air-Separator VSD (bypassable permissive) [A]

**Naming trap:** this instance is `FilterUnitInst3` and it is **Dust Filter Unit *2***; its signals
are the `FilterUnit2*` family. Verified: `FilterUnitInst3.ir` declares `DB FilterUnitInst3`.

## IO BINDING [io]

| Role | Signal | Status |
|---|---|---|
| start command out | `DiscreteOutputs.FilterUnit2Start` | EXISTS |
| fault-reset command out | `DiscreteOutputs.FilterUnit2Reset` | EXISTS |
| plant fault-reset command in | `HMIControlSignals.SystemReset` | EXISTS |
| unit fault feedback | `DiscreteInputs.FilterUnit2Flt` | EXISTS |
| running feedback | **unresolved** — see C14 / Q-C02 | — |
| remote-operational feedback | **unresolved** — see C5 / Q-C02 | — |
| isolation feedback | **NO SIGNAL** — no `FilterUnit2IsoFB` in `DiscreteInputs` | Q-C03 |
| pre-start-complete condition | `PlantControl.PreStartComplete` | EXISTS |
| plant run state | `PlantControl.Status` | EXISTS |
| fan up-to-speed time | `ProcessTimings.NormalFanStartTime` (10.0) | EXISTS |
| plant fans-shutdown-ready condition | `PlantControl.FansShutdownReady` | EXISTS |

`converter tagstatus` → 0 proposed, 0 member-unchecked for the bound names; the isolation signal is
absent by the same MEMBER-NOT-FOUND evidence recorded for `FilterUnitInst2`.

## CONTROL REQUIREMENTS (complete by construction from the class reference)

- **C1** the unit runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead [ref FU-01]
- **C2** the unit does not start while it is inhibited (isolated / locked off) [ref FU-02]
  → **no signal carries this for this instance.** **BLOCKING Q-C03.**
- **C3** the unit does not start while it has an active fault [ref FU-03]
- **C4** the unit does not start unless it is reported system-healthy [ref FU-04]
  → no signal: health is supervised outside this layer [B-32]. Recorded as a delta. Q-C20.
- **C5** the unit does not start unless it is in remote (not local) operating mode [ref FU-05]
      CANDIDATES: `DiscreteInputs.FilterUnit2Ready`
                  `DiscreteInputs.FilterUnit2Op`
                  `FilterUnitInst3.RemoteOp` [iface, named-only]
      → **BLOCKING Q-C02** — decision deferred to D1
- **C6** the unit does not start until the plant pre-start warning phase has completed
  [ref FU-06 + io `PlantControl.PreStartComplete`]
- **C7** once running and confirmed running for the fan up-to-speed time, the unit declares itself
  enabled, and that enable is what permits the machine it serves to start
  [ref FU-07 + io `ProcessTimings.NormalFanStartTime`]
- **C8** on a controlled shutdown request the unit stops, and declares its shutdown complete once
  the shutdown time has elapsed and its running feedback has cleared [ref FU-08]
- **C9** the unit also declares shutdown complete immediately if it is faulted, or if it is under
  hand control and the operator is not calling it to run [ref FU-09]
- **C10** if the unit is commanded to run and does not report running within the fault time, a
  fail-to-run fault is raised and latched [ref FU-10]
- **C11** if the unit reports running while not commanded to run for that same fault time, a
  fail-to-stop fault is raised and latched [ref FU-11]
- **C12** a fault feedback from the unit raises a latched fault
  [ref FU-12 + io `DiscreteInputs.FilterUnit2Flt`]
- **C13** a fault-reset command clears all latched faults, is taken from the single plant-wide
  operator reset, and is echoed back to the unit as a physical reset command
  [ref FU-13 + B-15 + B-16 + io `HMIControlSignals.SystemReset`, `DiscreteOutputs.FilterUnit2Reset`]
- **C14** the unit's actual running state is taken from its own running feedback; this class has no
  motion/rotation sensor [ref FU-14 + B-22]
      CANDIDATES: `DiscreteInputs.FilterUnit2Op`
                  `DiscreteInputs.FilterUnit2Ready`
                  `FilterUnitInst3.IO.RunningFB` [iface, named-only]
      → **BLOCKING Q-C02** — decision deferred to D1
- **C15** running hours are totalised for the unit [ref FU-15]
- **C16** a status indication is published for the operator [ref FU-16]
- **C17** *(alarm indication)* [ref FU-17] → **out of scope for this artifact** (alarms separate).
- **C18** the resulting run command drives the unit's physical start output
  [B-28 + io `DiscreteOutputs.FilterUnit2Start`]

## PLANT INTERLOCKS (fully enumerated, one relation per line)

- **P1** start permitted only while the Sorter Conveyor VSD (`MotorVSDInst3`) is enabled [A rel-5, B-08]
- **P2** on controlled shutdown, hold until the plant fans-shutdown-ready condition is present
  [A rel-6, B-06 + io `PlantControl.FansShutdownReady`]
- **P3** on controlled shutdown, hold until the Air-Separator VSD (`AirStarInst1`) reports its
  shutdown complete [A rel-7, B-05]
  → **two lines, not one**: different signals, so §5 forbids the merge. Combination → Q-C09.
- **P4** this unit's enable is one of the permissives for the Air-Separator VSD's start [A rel-8]
- **P5** the Air-Separator VSD has a second start path that does not require this unit's enable
  (discharge-conveyor drive enabled together with `InterlockData.LinkOut3rdParty`) [A delta, B-12]
- **P6** while the plant is in its running state, this unit is commanded to start and run
  automatically, subject to P1–P3 and C1–C7 [B-02 + io `PlantControl.Status`]
- **P7** while this unit is under hand intervention, its automatic controlled-shutdown request is
  suppressed [B-13]

## SETTINGS

| Setting | Owner | Value | Basis |
|---|---|---|---|
| fan up-to-speed (enable) time | engineering | **10 s** | stated [B-26, REQ-020] — `ProcessTimings.NormalFanStartTime` = 10.0 |
| shutdown-complete time | engineering | **not stated** | Q-C15 |
| fail-to-run / fail-to-stop time | engineering | **not stated** | Q-C15 |
| remote-operation mode | engineering | **not stated** | interface setpoint [ref FU-05]; field counterpart unresolved at C5 |

## UNCLAIMED IO (§8(a) per-instance sweep)

| Signal | Disposition |
|---|---|
| `DiscreteInputs.FilterUnit2Flt` | bound — C12 |
| `DiscreteInputs.FilterUnit2Op` | in the C5/C14 candidate set — **unresolved, Q-C02** |
| `DiscreteInputs.FilterUnit2Ready` | in the C5/C14 candidate set — **unresolved, Q-C02** |
| `DiscreteOutputs.FilterUnit2Start` | bound — C18 |
| `DiscreteOutputs.FilterUnit2Reset` | bound — C13 |
| `HMIControlSignals.HandFansShutdown` | **UNCLAIMED — BLOCKING Q-C04** (scoped to the fans, therefore to this unit; no rung-A relation or rung-B behaviour accounts for it) |
| `PlantControl.FansShutdownReady` | bound — P2 |

## DELTAS

- **PLUS** — the machine this unit serves has an alternative start path not requiring this unit's
  enable [A, B-12] → P5.
- **MINUS** — no machine holds its controlled shutdown on this unit's shutdown-complete.
- **MINUS** — its start permissive is not the machine it serves.
- **MINUS (new at this rung)** — no isolation signal exists while the class requires the permissive
  (C2/FU-02) → Q-C03.
- **NONE-FURTHER-FOUND — searched:** rung-A deltas; class reference §Class requirement set and
  §Class deltas; §8(a) sweep above; §8(b) plant residual sweep; `candidate-scan` IO sets for
  `DiscreteInputs.FilterUnit2` (3) and `DiscreteOutputs.FilterUnit2` (2); `tagstatus` on all bound
  and attempted names.
- **Identical-source-twin:** `FilterUnitInst2`.

## OPEN

- **Q-C02 (BLOCKING)** — running vs remote-operational binding, `FilterUnit2Ready` / `FilterUnit2Op`
- **Q-C03 (BLOCKING)** — no isolation signal for this unit
- **Q-C04 (BLOCKING)** — `HandFansShutdown` unclaimed and in this unit's scope
- **Q-C09 (BLOCKING)** — how P2 and P3 combine
- **Q-C15 (non-blocking)** — unstated time values
- **Q-C20 (BLOCKING, plant-level)** — plant-health residual signals
