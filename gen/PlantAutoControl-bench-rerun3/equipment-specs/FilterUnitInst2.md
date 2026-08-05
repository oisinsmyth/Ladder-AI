# EQUIPMENT SPEC — FilterUnitInst2 (Dust Filter Unit 1)

    class: FilterUnitSystem (reference v0-derived, 2026-08-04)
    fed-by: not-stated-by-source [A, Q-A4]      discharges-to: not-stated-by-source [A, Q-A4]
    serves: Air-Separator VSD (bypassable permissive) [A]

**Naming trap, recorded before anything else:** this instance is `FilterUnitInst2` and it is
**Dust Filter Unit *1***. Its signals are the `FilterUnit1*` family. The instance number and the unit
number are off by one, plant-wide, for both dust filters. Verified: `FilterUnitInst2.ir` declares
`DB FilterUnitInst2`; `FilterUnitInst3.ir` declares `DB FilterUnitInst3`.

## IO BINDING [io]

| Role | Signal | Status |
|---|---|---|
| start command out | `DiscreteOutputs.FilterUnit1Start` | EXISTS |
| fault-reset command out | `DiscreteOutputs.FilterUnit1Reset` | EXISTS |
| plant fault-reset command in | `HMIControlSignals.SystemReset` | EXISTS |
| unit fault feedback | `DiscreteInputs.FilterUnit1Flt` | EXISTS |
| running feedback | **unresolved** — see C14 / Q-C01 | — |
| remote-operational feedback | **unresolved** — see C5 / Q-C01 | — |
| isolation feedback | **NO SIGNAL** — `DiscreteInputs.FilterUnit1IsoFB` → MEMBER-NOT-FOUND | Q-C03 |
| pre-start-complete condition | `PlantControl.PreStartComplete` | EXISTS |
| plant run state | `PlantControl.Status` | EXISTS |
| fan up-to-speed time | `ProcessTimings.NormalFanStartTime` (10.0) | EXISTS |
| plant fans-shutdown-ready condition | `PlantControl.FansShutdownReady` | EXISTS |

`converter tagstatus --project ir/PlantAutoControl-bench <all of the above>` → 0 proposed,
1 member-not-found (`FilterUnit1IsoFB`), 0 member-unchecked.

## CONTROL REQUIREMENTS (complete by construction from the class reference)

- **C1** the unit runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead [ref FU-01]
- **C2** the unit does not start while it is inhibited (isolated / locked off) [ref FU-02]
  → **no signal carries this for this instance** — `DiscreteInputs.FilterUnit1IsoFB` is
  MEMBER-NOT-FOUND and no other isolation signal is scoped to this unit.
  **BLOCKING Q-C03.** Not dropped, not invented.
- **C3** the unit does not start while it has an active fault [ref FU-03]
- **C4** the unit does not start unless it is reported system-healthy [ref FU-04]
  → no signal: this layer asserts every machine healthy and plant health is supervised elsewhere
  [B-32]. Recorded as a **delta**, not a silent absence. The residual plant-health signals are
  raised at plant level (`unclaimed-signals.md`, Q-C20).
- **C5** the unit does not start unless it is in remote (not local) operating mode [ref FU-05]
      CANDIDATES: `DiscreteInputs.FilterUnit1Ready`
                  `DiscreteInputs.FilterUnit1Op`
                  `FilterUnitInst2.RemoteOp` [iface, named-only]
      → **BLOCKING Q-C01** — decision deferred to D1
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
  [ref FU-12 + io `DiscreteInputs.FilterUnit1Flt`]
- **C13** a fault-reset command clears all latched faults, is taken from the single plant-wide
  operator reset, and is echoed back to the unit as a physical reset command
  [ref FU-13 + B-15 + B-16 + io `HMIControlSignals.SystemReset`, `DiscreteOutputs.FilterUnit1Reset`]
- **C14** the unit's actual running state is taken from its own running feedback; this class has no
  motion/rotation sensor [ref FU-14 + B-22 (named as a machine without one)]
      CANDIDATES: `DiscreteInputs.FilterUnit1Op`
                  `DiscreteInputs.FilterUnit1Ready`
                  `FilterUnitInst2.IO.RunningFB` [iface, named-only]
      → **BLOCKING Q-C01** — decision deferred to D1
- **C15** running hours are totalised for the unit [ref FU-15]
- **C16** a status indication is published for the operator (faulted / stopped / commanded /
  running / enabled) [ref FU-16]
- **C17** *(alarm indication — fail-to-run, fail-to-stop, unit fault, not-in-remote)* [ref FU-17]
  → **out of scope for this artifact by this rung's calibration** (alarms are a separate artifact).
  Recorded so it is visibly excluded rather than lost.
- **C18** the resulting run command drives the unit's physical start output
  [B-28 + io `DiscreteOutputs.FilterUnit1Start`]

## PLANT INTERLOCKS (fully enumerated, one relation per line)

- **P1** start permitted only while the Sorter Conveyor VSD (`MotorVSDInst3`) is enabled [A rel-1, B-08]
- **P2** on controlled shutdown, hold until the plant fans-shutdown-ready condition is present
  [A rel-2, B-06 + io `PlantControl.FansShutdownReady`]
- **P3** on controlled shutdown, hold until the Air-Separator VSD (`AirStarInst1`) reports its
  shutdown complete [A rel-3, B-05]
  → **P2 and P3 are deliberately two lines.** They are both hold conditions, they resolve to
  **different signals** (a plant-level flag vs a specific neighbour's shutdown-complete), and §5
  permits a merge only when both resolve to the same signal or the same named plant condition.
  Collapsing them here would destroy the evidence D2 needs. **How they combine is Q-C09 (BLOCKING).**
- **P4** this unit's enable is one of the permissives for the Air-Separator VSD's start [A rel-4]
- **P5** the Air-Separator VSD has a second start path that does not require this unit's enable
  (discharge-conveyor drive enabled together with `InterlockData.LinkOut3rdParty`) [A delta, B-12]
- **P6** while the plant is in its running state, this unit is commanded to start and run
  automatically, subject to P1–P3 and C1–C7 [B-02 + io `PlantControl.Status`]
- **P7** while this unit is under hand intervention, its automatic controlled-shutdown request is
  suppressed [B-13]

## SETTINGS

| Setting | Owner | Value | Basis |
|---|---|---|---|
| fan up-to-speed (enable) time | engineering | **10 s** | stated by source [B-26, REQ-020] — `ProcessTimings.NormalFanStartTime` = 10.0 |
| shutdown-complete time | engineering | **not stated** | Q-C15 (carries B's Q-B6) |
| fail-to-run / fail-to-stop time | engineering | **not stated** | Q-C15 |
| remote-operation mode | engineering | **not stated** | held on the equipment interface as a setpoint [ref FU-05]; its value is not given by any source, and its *field* counterpart is the unresolved C5 binding |

No value here is `reference-proposed`; the class reference proposes none for this instance, and none
was invented.

## UNCLAIMED IO (§8(a) per-instance sweep)

Signals scoped to this instance, swept in the opposite direction to the binding pass:

| Signal | Disposition |
|---|---|
| `DiscreteInputs.FilterUnit1Flt` | bound — C12 |
| `DiscreteInputs.FilterUnit1Op` | in the C5/C14 candidate set — **unresolved, Q-C01** |
| `DiscreteInputs.FilterUnit1Ready` | in the C5/C14 candidate set — **unresolved, Q-C01** |
| `DiscreteOutputs.FilterUnit1Start` | bound — C18 |
| `DiscreteOutputs.FilterUnit1Reset` | bound — C13 |
| `HMIControlSignals.HandFansShutdown` | **UNCLAIMED — BLOCKING Q-C04.** An operator command whose name scopes it to the fans, i.e. to this unit and its two siblings. No behaviour at rung B and no relation at rung A accounts for it. It plainly implies a control function (an operator-commanded shutdown of the filter units), and §8's rule is that a residual signal implying a control function is blocking regardless of its name. |
| `PlantControl.FansShutdownReady` | bound — P2 |

## DELTAS

- **PLUS** — the machine this unit serves has an alternative start path that does not require this
  unit's enable, so the enable is a bypassable permissive [A, B-12] → P5.
- **MINUS** — no machine holds its controlled shutdown on this unit's shutdown-complete (full 20-row
  sweep at rung A). Terminal in the shutdown chain, non-terminal in the start chain.
- **MINUS** — its start permissive (Sorter Conveyor VSD) is not the machine it serves (Air-Separator
  VSD): the chain through this unit is not a single line.
- **MINUS (new at this rung)** — **no isolation signal exists for this unit** while its class
  requires the permissive (C2/FU-02). Invisible at rungs A and B; found by `tagstatus` returning
  MEMBER-NOT-FOUND. → Q-C03.
- **NONE-FURTHER-FOUND — searched:** rung-A deltas for this instance; class reference §Class
  requirement set (FU-01..FU-17) and §Class deltas; §8(a) sweep above; §8(b) plant residual sweep;
  `candidate-scan` IO sets for `DiscreteInputs.FilterUnit1` (3) and `DiscreteOutputs.FilterUnit1` (2);
  `tagstatus` on all eleven bound/attempted names.
- **Identical-source-twin:** `FilterUnitInst3`. Every rung-A source column and every class
  requirement is the same; the two differ only in their signal family (`FilterUnit1*` vs
  `FilterUnit2*`). Stated as a recorded fact for rung D's grouping decision, not as the decision.

## OPEN

- **Q-C01 (BLOCKING)** — running vs remote-operational binding, `FilterUnit1Ready` / `FilterUnit1Op`
- **Q-C03 (BLOCKING)** — no isolation signal for this unit (C2 unsourced)
- **Q-C04 (BLOCKING)** — `HMIControlSignals.HandFansShutdown` unclaimed and in this unit's scope
- **Q-C09 (BLOCKING)** — how P2 and P3 combine (carries Q-A1 / Q-B7)
- **Q-C15 (non-blocking)** — no values stated for shutdown / fail-to-run / fail-to-stop times
- **Q-C20 (BLOCKING, plant-level)** — plant-health residual signals (C4 has no source)
