# EQUIPMENT SPEC — Dust Filter Unit 1 (`FilterUnitInst2`)

Rung C (`/gen-equipment-spec`), run `PlantAutoControl-bench-rerun2`, 2026-08-05.
**Signals are bound here. Booleans, interface members and network structure are NOT** — those are
rung D. The one carve-out is the `CANDIDATES:` field, which may name an interface member
`[iface, named-only]`; naming a candidate is never a binding.

```
class: FilterUnitSystem (ref v0-derived 2026-08-04)   FB type: FilterUnitSystem
fed-by:        NOT ESTABLISHED — no material-flow source (Q-A03)
discharges-to: NOT ESTABLISHED — no material-flow source (Q-A03)
serves:        Air-Separator VSD (enable-wise) [A]
```

**Tag verification method:** every signal below was grep-verified member-by-member against
`ir/PlantAutoControl-bench/*.ir`. `converter tagstatus` was run and reported 0 proposed, **but its
verdict is not relied on** — it validates only the root DB name, not the member path (it reports
`HMIControlSignals.TotallyInventedMember -> EXISTS`). See the run report.

## IO BINDING [io]

| Role | Signal | Verified |
|---|---|---|
| run command → field | `DiscreteOutputs.FilterUnit1Start` | grep ✓ |
| fault reset → field | `DiscreteOutputs.FilterUnit1Reset` | grep ✓ |
| unit fault report | `DiscreteInputs.FilterUnit1Flt` | grep ✓ |
| running confirm / remote-operational | **UNRESOLVED** — `DiscreteInputs.FilterUnit1Op`, `DiscreteInputs.FilterUnit1Ready` | grep ✓ (both) |
| plant run state | `PlantControl.Status` | grep ✓ |
| plant pre-start complete | `PlantControl.PreStartComplete` | grep ✓ |
| plant fans-shutdown-ready | `PlantControl.FansShutdownReady` | grep ✓ |
| plant-wide operator reset | `HMIControlSignals.SystemReset` | grep ✓ |
| fan start-up time setting | `ProcessTimings.NormalFanStartTime` (= 10.0) | grep ✓ |
| inhibit / isolator | **NO SIGNAL EXISTS** — Q-C03 | — |
| hand/auto mode source | **NO SIGNAL EXISTS** — Q-C04 | — |
| system-healthy source | **UNRESOLVED** — Q-C05 | — |

## CONTROL REQUIREMENTS (complete by construction from the class reference, FU-01…FU-17)

- **C1** the unit runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead `[ref FU-01]`
  **CANDIDATES:** *(no signal in the exported IO carries this unit's hand/auto state)*
  `InterlockData.PartInHand` — plant-level, wrong granularity, would apply to every machine at once
  `FilterUnitInst2.IO.InHand` `[iface, named-only]` · `FilterUnitInst2.IO.HandStartSignal`
  `[iface, named-only]`
  → **BLOCKING Q-C04** — the per-machine in-hand source is a **proposed** signal; decision deferred
  to D1.
- **C2** the unit does not start while it is inhibited (isolated / locked off) `[ref FU-02]`
  **CANDIDATES:** *(empty — no FilterUnitSystem isolator signal exists; the IO table has isolator
  feedbacks for fourteen other machines and none for any filter unit)*
  → **BLOCKING Q-C03** — signal **proposed**, never invented.
- **C3** the unit does not start while it has an active fault `[ref FU-03]` — fault sourced from
  `DiscreteInputs.FilterUnit1Flt` `[ref+io]`
- **C4** the unit does not start unless it is reported system-healthy `[ref FU-04]`
  **CANDIDATES:** `DiscreteInputs.ControlHealthy` — plant-level health input, claimed by no
  instance · *a hard-asserted constant true, per `[B-34]`*
  → **BLOCKING Q-C05** — a permissive satisfied by a constant is not a permissive; both readings
  recorded, neither chosen.
- **C5** the unit does not start unless it is in remote (not local) operating mode `[ref FU-05]`
  **CANDIDATES:** `DiscreteInputs.FilterUnit1Ready` · `DiscreteInputs.FilterUnit1Op` ·
  `FilterUnitInst2.RemoteOp` `[iface, named-only]` (a static setpoint on the class, not a field
  signal)
  → **BLOCKING Q-C01** — see C11; this requirement and C11 compete for the same two field signals.
- **C6** the unit does not start until the plant pre-start warning phase has completed
  `[ref FU-06 + io]` — `PlantControl.PreStartComplete`
- **C7** once running and confirmed running for a configurable up-to-speed time, the unit declares
  itself enabled, which is what permits the machine it serves to start `[ref FU-07 + io]` —
  time from `ProcessTimings.NormalFanStartTime` (10.0 s)
- **C8** on a controlled shutdown request the unit stops, and declares its shutdown complete once a
  configurable shutdown time has elapsed and its running feedback has cleared `[ref FU-08]`
- **C9** the unit also declares shutdown complete immediately if it is faulted, or if it is under
  hand control and the operator is not calling it to run `[ref FU-09]`
- **C10** if the unit is commanded to run but does not report running within a configurable fault
  time, a fail-to-run fault is raised and latched `[ref FU-10]`
- **C11** if the unit reports running while not commanded to run for that same fault time, a
  fail-to-stop fault is raised and latched `[ref FU-11]`
- **C12** a fault feedback from the unit itself raises a latched fault `[ref FU-12 + io]` —
  `DiscreteInputs.FilterUnit1Flt`
- **C13** a fault-reset command clears all latched faults `[ref FU-13 + io]` —
  `HMIControlSignals.SystemReset`
- **C14** the unit's actual running state is taken from its own running feedback; this class has no
  motion/rotation sensor `[ref FU-14]`
  **CANDIDATES:** `DiscreteInputs.FilterUnit1Op` · `DiscreteInputs.FilterUnit1Ready`
  → **BLOCKING Q-C01** — *two requirements (this one and C5) competing for two same-shaped signals
  is always a candidate set, never a coin flip.* `…Op` reads as "operational" and `…Ready` as
  "ready", and either could carry either meaning; the class needs one *running* report and one
  *remote-operational* report. Decision deferred to D1.
- **C15** running hours are totalised for the unit `[ref FU-15]`
- **C16** a status indication is published for the operator (faulted / stopped / commanded /
  running / enabled) `[ref FU-16]`
- **C17** an alarm indication is published for fail-to-run, fail-to-stop, unit fault feedback, and
  unit not in remote `[ref FU-17]` — *recorded for completeness; alarms are out of scope for this
  control spec by the rung's own calibration, and belong to the separate alarm artifact.*

## PLANT INTERLOCKS (fully enumerated, one relation per line)

- **P1** start permitted only while the Sorter Conveyor VSD (`MotorVSDInst3`) is enabled
  `[A + B-08]`
- **P2** on controlled shutdown, hold until the plant reports the filter fans ready to shut down
  `[A + B-17 + io]` — `PlantControl.FansShutdownReady`  **(Q-C12)**
- **P3** on controlled shutdown, hold until the Air-Separator VSD (`AirStarInst1`) reports its
  shutdown complete `[A + B-17]`  **(Q-C12)**
  > **P2 and P3 are deliberately two lines.** They are both hold conditions, both must be
  > represented, and they resolve to **different** signals — a plant-level flag and a specific
  > neighbour's shutdown-complete. Collapsing them here would destroy the evidence rung D's ledger
  > needs. **How they combine (both / either) is not settled by any source — Q-C12, blocking.**
- **P4** start permitted only while the plant is in its running state `[A + B-02 + io]` —
  `PlantControl.Status`
- **P5** start permitted only after the plant pre-start warning phase has completed
  `[A + B-05 + io]` — `PlantControl.PreStartComplete` *(same fact as C6; merged and both cited)*
- **P6** while this unit is under hand intervention, the automatic controlled-shutdown request for
  it is suppressed `[A + B-19]`
- **P7** this unit's latched faults are cleared by the single plant-wide operator reset
  `[A + B-24 + io]` — `HMIControlSignals.SystemReset` *(same fact as C13; merged and both cited)*
- **P8** the plant-wide reset is additionally echoed to the unit as a physical reset command
  `[A delta + B-25 + io]` — `DiscreteOutputs.FilterUnit1Reset`
- **P9** this unit's enable is one of the start permissives for the Air-Separator VSD, on that
  machine's primary start path only `[A + B-44]`
- **P10** the unit's resulting run command drives its physical start output `[B-40 + io]` —
  `DiscreteOutputs.FilterUnit1Start`

## SETTINGS

| Setting | Owner | Value | Provenance |
|---|---|---|---|
| fan start-up (up-to-speed) time | engineering | **10.0 s** | `ProcessTimings.NormalFanStartTime` `[io]`, and stated in `[B-43]` |
| shutdown time before shutdown-complete | engineering | **5.0 s** | given instance-DB start value `(boundary)` — **not** stated in any plant document `[Q-B21]` |
| fail-to-run / fail-to-stop fault time | engineering | **2.0 s** | given instance-DB start value `(boundary)` — not in any plant document `[Q-B21]`. *Differs from Dust Filter Unit 2's 3.0 s for two units the sources describe identically — Q-C23.* |
| running-hours totaliser interval | engineering | 1 h | given instance-DB start value `(boundary)` |
| remote-operating-mode permissive | engineering | static setpoint, value not initialised | given instance-DB `(boundary)`; interacts with Q-C01 |

## UNCLAIMED IO (§8a per-instance sweep)

Every signal scoped to this instance by name, and its disposition:

| Signal | Disposition |
|---|---|
| `DiscreteInputs.FilterUnit1Ready` | **claimed but unresolved** — in the C5/C14 candidate set (Q-C01) |
| `DiscreteInputs.FilterUnit1Op` | **claimed but unresolved** — in the C5/C14 candidate set (Q-C01) |
| `DiscreteInputs.FilterUnit1Flt` | bound — C3, C12 |
| `DiscreteOutputs.FilterUnit1Start` | bound — P10 |
| `DiscreteOutputs.FilterUnit1Reset` | bound — P8 |
| *(no `FilterUnit1IsoFB`)* | **absent** — Q-C03 |
| *(no `FilterUnit1RotSen`)* | correctly absent — the class has no motion sensor (C14) |
| `HMIControlSignals.HandFansShutdown` | **UNCLAIMED, control-implying** — an operator command whose name applies to fan/filter shutdown. No requirement in any source claims it. Escalated to the plant-level sweep as **BLOCKING Q-C16**. |
| `ProcessTimings.AntiCondensationTime`, `HMIControlSignals.AntiConEnabled`, `RunAntiConNxtStart`, `SkipAntiConNxtStart`, `PlantControl.AntiConRunRequired` | **UNCLAIMED, control-implying** — a whole anti-condensation feature set that plausibly scopes to fans/filters. No source mentions it. Escalated as **BLOCKING Q-C24**. |
| `HMIControlSignals.GlobalUPSEnableTimeOverwrite` + `ProcessTimings.GlobalUPEnableTime` | **UNCLAIMED, control-implying** — a plant-level override of exactly the up-to-speed time C7 uses. Escalated as **BLOCKING Q-C15**. |
| `HMIControlSignals.GlobalFTTimeOverwrite` + `ProcessTimings.GlobalFTTimes`, `GlobalShutdownCompleteTimeOverwrite` + `GlobalShutdownCompleteTime` | **UNCLAIMED, control-implying** — same, for C10/C11 and C8. **Q-C15**. |
| `HMIControlSignals.GlobalSetAllToAuto` | **UNCLAIMED, control-implying** — a plant-wide mode command reaching this unit. **Q-C25**. |

## DELTAS

Carried from rung A, each attached to the requirement it modifies. **Not** dropped in the merge.

- **Δ1 (+) on P8** — the plant-wide reset is echoed to the unit as a physical reset command; the
  class only requires the reset to clear latched faults internally. Signal bound.
- **Δ2 (?) on C5/C14** — the unit reports an operational-availability state (`…Op`) *and* a ready
  state (`…Ready`) from the field, where the class names one running feedback and one remote-mode
  permissive. Whether these are the same two things is Q-C01.
- **Δ3 (−) on C2** — the unit has no isolator feedback although the class requires an inhibit
  permissive and fourteen other machines in this plant have one. Q-C03.

**DELTAS: none-further-found** — searched: rung-A delta block for this instance, `FilterUnitSystem`
reference §"Class requirement set" and §"Class deltas", rung-B behaviours scoped to this instance
(B-02, 05, 09, 12, 17, 19, 23, 24, 25, 29, 35, 39, 40, 42, 43, 44, 50, 51, 52), the §8a sweep above,
and the §8b plant residual list.

## OPEN

**Blocking:** Q-C01 (running vs remote-operational candidate set), Q-C03 (no isolator signal),
Q-C04 (no hand/auto source), Q-C05 (system-healthy candidate set), Q-C12 (P2/P3 combination),
Q-C15 (global time overrides unclaimed), Q-C16 (`HandFansShutdown` unclaimed), Q-C24
(anti-condensation feature set unclaimed), Q-C25 (`GlobalSetAllToAuto` unclaimed).
**Carried, still blocking:** Q-A01, Q-A03, Q-A04, Q-B01, Q-B07, Q-B12.
**Non-blocking:** Q-C23 (fault time differs from the sibling unit), Q-A08, Q-A09, Q-B21.
