# EQUIPMENT SPEC — Cyclone Filter Unit (`FilterUnitInst1`)

Rung C (`/gen-equipment-spec`), run `PlantAutoControl-bench-rerun2`, 2026-08-05.
**Signals are bound here. Booleans, interface members and network structure are NOT.**

```
class: FilterUnitSystem (ref v0-derived 2026-08-04)   FB type: FilterUnitSystem
fed-by:        NOT ESTABLISHED — no material-flow source (Q-A03)
discharges-to: NOT ESTABLISHED — no material-flow source (Q-A03)
serves:        NOT ESTABLISHED — no machine names this unit's enable (Q-A06, blocking)
```

**Tag verification method:** grep-verified member-by-member against `ir/PlantAutoControl-bench/*.ir`;
`converter tagstatus`'s pass is not relied on (root-only checking).

## IO BINDING [io]

| Role | Signal | Verified |
|---|---|---|
| run command → field | `DiscreteOutputs.CycloneDustFilterStart` | grep ✓ |
| fault reset → field | `DiscreteOutputs.CycloneDustFilterReset` | grep ✓ |
| system-OK report (fault is its absence) | `DiscreteInputs.CycloneDustSysOk` | grep ✓ |
| remote-operational report | `DiscreteInputs.CycloneDustRemOp` | grep ✓ |
| running / stopped report | **AMBIGUOUS SINGLE SIGNAL** — `DiscreteInputs.CycloneDustAutoRunning/Stop` | grep ✓ |
| plant run state | `PlantControl.Status` | grep ✓ |
| plant pre-start complete | `PlantControl.PreStartComplete` | grep ✓ |
| plant fans-shutdown-ready | `PlantControl.FansShutdownReady` | grep ✓ |
| plant-wide operator reset | `HMIControlSignals.SystemReset` | grep ✓ |
| fan start-up time setting | `ProcessTimings.NormalFanStartTime` (= 10.0) | grep ✓ |
| unit fault report | **NO DIRECT SIGNAL** — derived from the absence of system-OK (Δ1) | — |
| inhibit / isolator | **NO SIGNAL EXISTS** — Q-C03 | — |
| hand/auto mode source | **NO SIGNAL EXISTS** — Q-C04 | — |
| system-healthy source | **UNRESOLVED** — Q-C05 | — |

## CONTROL REQUIREMENTS (complete by construction, FU-01…FU-17)

- **C1** the unit runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead `[ref FU-01]`
  **CANDIDATES:** `InterlockData.PartInHand` (plant-level, wrong granularity) ·
  `FilterUnitInst1.IO.InHand` `[iface, named-only]` → **BLOCKING Q-C04**
- **C2** the unit does not start while it is inhibited (isolated / locked off) `[ref FU-02]`
  **CANDIDATES:** *(empty)* → **BLOCKING Q-C03**
- **C3** the unit does not start while it has an active fault `[ref FU-03]` — fault derived from the
  absence of `DiscreteInputs.CycloneDustSysOk` (Δ1) `[ref+io+B-30]`
- **C4** the unit does not start unless it is reported system-healthy `[ref FU-04]`
  **CANDIDATES:** `DiscreteInputs.ControlHealthy` · `DiscreteInputs.CycloneDustSysOk` (a
  unit-level health report — plausibly this requirement rather than C3's fault source) · *a
  hard-asserted constant true per `[B-34]`*
  → **BLOCKING Q-C27** — *three candidates, and one of them (`CycloneDustSysOk`) is already claimed
  by C3. A unit that reports "system OK" satisfies the phrase "reported system-healthy" at least as
  well as it satisfies "has an active fault, inverted".* Decision deferred to D1.
- **C5** the unit does not start unless it is in remote (not local) operating mode `[ref FU-05]`
  **CANDIDATES:** `DiscreteInputs.CycloneDustRemOp` · `FilterUnitInst1.RemoteOp`
  `[iface, named-only]` (static setpoint)
  → *single field candidate; bound to `DiscreteInputs.CycloneDustRemOp` on a documentary basis —
  the source names it as this unit's remote-operational report (`[B-42]`, REQ-019) and no other
  signal in the export carries a remote/local state for any filter unit.* **Not blocking.**
  *Note: this is the only one of the three filter units whose remote-mode signal is unambiguous;
  the two dust units have the competing `…Op`/`…Ready` pair instead (Q-C01/Q-C02) — an asymmetry
  worth an owner's eye.*
- **C6** the unit does not start until the plant pre-start warning phase has completed
  `[ref FU-06 + io]` — `PlantControl.PreStartComplete`
- **C7** once running and confirmed running for a configurable up-to-speed time, the unit declares
  itself enabled, which permits the machine it serves to start `[ref FU-07 + io]` —
  `ProcessTimings.NormalFanStartTime` (10.0 s).
  **No machine is served (Q-A06/Q-C20)** — so this requirement's stated purpose is unfulfilled for
  this instance while its timing setting is nonetheless configured. Recorded, not resolved.
- **C8** on a controlled shutdown request the unit stops, and declares its shutdown complete once a
  configurable shutdown time has elapsed and its running feedback has cleared `[ref FU-08]`
- **C9** the unit also declares shutdown complete immediately if it is faulted, or if it is under
  hand control and the operator is not calling it to run `[ref FU-09]`
- **C10** a fail-to-run fault is raised and latched if the unit is commanded but does not report
  running within a configurable fault time `[ref FU-10]`
- **C11** a fail-to-stop fault is raised and latched if the unit reports running while not commanded
  for that same fault time `[ref FU-11]`
- **C12** a fault feedback from the unit itself raises a latched fault `[ref FU-12]` — **this class
  requirement has no direct signal on this instance**; it is satisfied only through Δ1's inverted
  system-OK report. Recorded as a delta, not a silent absence.
- **C13** a fault-reset command clears all latched faults `[ref FU-13 + io]` —
  `HMIControlSignals.SystemReset`
- **C14** the unit's actual running state is taken from its own running feedback; this class has no
  motion/rotation sensor `[ref FU-14]`
  **CANDIDATES:** `DiscreteInputs.CycloneDustAutoRunning/Stop` — **one signal whose name asserts two
  different meanings** ("auto running" and "stop")
  → **BLOCKING Q-C06.** *Split-and-retain is FORBIDDEN here by the explicitness carve-out: retaining
  both readings as two conditions would assert two channels where the IO table has exactly one
  member. Whether this signal means "the unit is running in auto", "the unit has stopped", or is a
  single two-state report whose polarity is unstated, is not derivable — and the two readings are
  inverses of each other, so guessing has a 50% chance of inverting the unit's running state.*
- **C15** running hours are totalised `[ref FU-15]`
- **C16** a status indication is published for the operator `[ref FU-16]`
- **C17** an alarm indication is published for fail-to-run, fail-to-stop, unit fault feedback, and
  not-in-remote `[ref FU-17]` — *alarms out of scope for this control spec.*

## PLANT INTERLOCKS

- **P1** start permissive — **NOT ESTABLISHED.** No neighbouring machine's enable gates this unit's
  start; the source records only "(runs with plant)". `[A]` → **BLOCKING Q-C19** (carries Q-A05).
  *Recorded as a relation line with an unresolved right-hand side, not omitted: an omitted line is
  indistinguishable downstream from a machine that genuinely has no permissive.*
- **P2** on controlled shutdown, hold until the plant reports the filter fans ready to shut down
  `[A + B-17 + io]` — `PlantControl.FansShutdownReady`  **(Q-C12)**
- **P3** on controlled shutdown, hold until the Shredder (`ShredderControlInst1`) reports its
  shutdown complete `[A + B-17]`  **(Q-C12)**
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
  `[A delta + B-25 + io]` — `DiscreteOutputs.CycloneDustFilterReset`
- **P9** served machine — **NOT ESTABLISHED** `[A]` → **BLOCKING Q-C20** (carries Q-A06).
- **P10** the unit's resulting run command drives its physical start output `[B-40 + io]` —
  `DiscreteOutputs.CycloneDustFilterStart`

## SETTINGS

| Setting | Owner | Value | Provenance |
|---|---|---|---|
| fan start-up (up-to-speed) time | engineering | **10.0 s** | `ProcessTimings.NormalFanStartTime` `[io]`, `[B-43]` |
| shutdown time | engineering | **5.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| fail-to-run / fail-to-stop fault time | engineering | **60.0 s** | given instance-DB start value `(boundary)`. **Twenty to thirty times the dust units' 2–3 s** for the same class. Plausible for a large cyclone fan, but stated nowhere — **Q-C28** (non-blocking). |
| running-hours totaliser interval | engineering | 1 h | given instance-DB start value `(boundary)` |
| remote-operating-mode permissive | engineering | static setpoint, value not initialised | given instance-DB `(boundary)` |

## UNCLAIMED IO (§8a per-instance sweep)

| Signal | Disposition |
|---|---|
| `DiscreteInputs.CycloneDustSysOk` | bound — C3 (and a candidate for C4, Q-C27) |
| `DiscreteInputs.CycloneDustRemOp` | bound — C5 |
| `DiscreteInputs.CycloneDustAutoRunning/Stop` | claimed but unresolved — C14 (Q-C06) |
| `DiscreteOutputs.CycloneDustFilterStart` | bound — P10 |
| `DiscreteOutputs.CycloneDustFilterReset` | bound — P8 |
| *(no `CycloneDust*IsoFB`)* | absent — Q-C03 |
| *(no `CycloneDust*Flt`)* | absent by design — Δ1 |
| `HMIControlSignals.HandFansShutdown` | UNCLAIMED, control-implying — **Q-C16** (plant-level) |
| anti-condensation set | UNCLAIMED, control-implying — **Q-C24** (plant-level) |
| global time-override set | UNCLAIMED, control-implying — **Q-C15** (plant-level) |
| `HMIControlSignals.GlobalSetAllToAuto` | UNCLAIMED, control-implying — **Q-C25** (plant-level) |

## DELTAS

- **Δ1 (Δ) on C3/C12** — this unit's fault is derived from the **absence** of a system-OK report
  rather than from a fault report. Failure mode differs: a dead reporting path reads as faulted.
  `[A + B-30]`
- **Δ2 (Δ) on C14** — this unit reports running and stopped through a single ambiguously-named
  signal rather than the class's plain running feedback. Q-C06.
- **Δ3 (+) on P8** — plant-wide reset echoed to the unit as a physical reset command.
- **Δ4 (?) on C5** — the unit brings a remote-operational report in from the field; the class's
  remote permissive is a static setpoint. Bound on documentary basis; the *relationship* between the
  field report and the setpoint is unresolved (Q-A08).
- **Δ5 (−) on P1** — no downstream-ready start permissive. Q-C19.
- **Δ6 (−) on C7/P9** — the unit's enable serves no machine, so the class's core enable purpose is
  unfulfilled here while its timing setting is configured. Q-C20.
- **Δ7 (−) on C2** — no isolator feedback. Q-C03.

**DELTAS: none-further-found** — searched: rung-A delta block for this instance, `FilterUnitSystem`
reference §"Class requirement set" and §"Class deltas", rung-B behaviours scoped to this instance
(B-02, 05, 09, 12, 17, 19, 23, 24, 25, 29, 30, 35, 39, 40, 42, 43, 50, 51, 52), the §8a sweep above,
the §8b plant residual list, and a field comparison against the two dust filter units.

## OPEN

**Blocking:** Q-C03, Q-C04, Q-C05, Q-C06, Q-C12, Q-C15, Q-C16, Q-C19, Q-C20, Q-C24, Q-C25, Q-C27.
**Carried, still blocking:** Q-A01, Q-A03, Q-A04, Q-A05, Q-A06, Q-B01, Q-B07, Q-B12.
**Non-blocking:** Q-C28, Q-A07, Q-A08, Q-B21.
