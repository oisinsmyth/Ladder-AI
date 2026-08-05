# EQUIPMENT SPEC — FilterUnitInst1 (Cyclone Filter Unit)

    class: FilterUnitSystem (reference v0-derived, 2026-08-04)
    fed-by: not-stated-by-source [A, Q-A4]      discharges-to: not-stated-by-source [A, Q-A4]
    serves: NOBODY — no machine's start permissive names this unit's enable [A, Q-A7]

## IO BINDING [io]

| Role | Signal | Status |
|---|---|---|
| start command out | `DiscreteOutputs.CycloneDustFilterStart` | EXISTS |
| fault-reset command out | `DiscreteOutputs.CycloneDustFilterReset` | EXISTS |
| plant fault-reset command in | `HMIControlSignals.SystemReset` | EXISTS |
| remote-operational feedback | `DiscreteInputs.CycloneDustRemOp` | EXISTS |
| running feedback | `DiscreteInputs.CycloneDustAutoRunning/Stop` | EXISTS — **semantics unresolved, Q-C05** |
| system-OK feedback (carries the fault condition) | `DiscreteInputs.CycloneDustSysOk` | EXISTS |
| isolation feedback | **NO SIGNAL** — no `CycloneDust*IsoFB` in `DiscreteInputs` | Q-C03 |
| pre-start-complete condition | `PlantControl.PreStartComplete` | EXISTS |
| plant run state | `PlantControl.Status` | EXISTS |
| fan up-to-speed time | `ProcessTimings.NormalFanStartTime` (10.0) | EXISTS |
| plant fans-shutdown-ready condition | `PlantControl.FansShutdownReady` | EXISTS |

**Why this unit's `remote-operational` / `running` pair is bound while the two dust filters' is not.**
`candidate-scan` returned a 3-signal IO set here too, so a choice existed and had to be shown. The
documentary basis for taking it: the names carry their roles explicitly and disjointly —
`CycloneDustRemOp` states *remote-operational*, `CycloneDustAutoRunning/Stop` states *running*, and
`CycloneDustSysOk` states *system OK*. Each of the three class roles (FU-05, FU-14, FU-12) matches
exactly one name, with no role matching two. That is a documentary basis in the naming, and it is
cited rather than assumed. The dust filters' `Ready`/`Op` pair has no such disjoint mapping, which is
precisely why Q-C01/Q-C02 are blocking and this one is not.
**Note the contrast is itself evidence:** the same plant names the same class role `RemOp` on one
unit and (possibly) `Op` on another. That inconsistency is why Q-C01/Q-C02 cannot be closed by
analogy to this unit.

## CONTROL REQUIREMENTS (complete by construction from the class reference)

- **C1** the unit runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead [ref FU-01]
- **C2** the unit does not start while it is inhibited (isolated / locked off) [ref FU-02]
  → **no signal carries this for this instance.** **BLOCKING Q-C03.**
- **C3** the unit does not start while it has an active fault [ref FU-03]
- **C4** the unit does not start unless it is reported system-healthy [ref FU-04]
  → no signal; health supervised outside this layer [B-32]. Delta. Q-C20.
- **C5** the unit does not start unless it is in remote (not local) operating mode
  [ref FU-05 + io `DiscreteInputs.CycloneDustRemOp`]
- **C6** the unit does not start until the plant pre-start warning phase has completed
  [ref FU-06 + io `PlantControl.PreStartComplete`]
- **C7** once running and confirmed running for the fan up-to-speed time, the unit declares itself
  enabled [ref FU-07 + io `ProcessTimings.NormalFanStartTime`]
  → **this enable permits no machine in the given topology** [A, Q-A7]. Retained in full because the
  class requires it; the missing consumer is the recorded delta, not a reason to drop the
  requirement.
- **C8** on a controlled shutdown request the unit stops, and declares its shutdown complete once
  the shutdown time has elapsed and its running feedback has cleared [ref FU-08]
- **C9** the unit also declares shutdown complete immediately if it is faulted, or if it is under
  hand control and the operator is not calling it to run [ref FU-09]
- **C10** if the unit is commanded to run and does not report running within the fault time, a
  fail-to-run fault is raised and latched [ref FU-10]
- **C11** if the unit reports running while not commanded to run for that same fault time, a
  fail-to-stop fault is raised and latched [ref FU-11]
- **C12** a fault condition from the unit raises a latched fault. This unit has no direct fault
  feedback: the fault condition is carried by `DiscreteInputs.CycloneDustSysOk` in its healthy
  sense — system-OK asserted means no fault
  [ref FU-12 + B-18 + io `DiscreteInputs.CycloneDustSysOk`]
- **C13** a fault-reset command clears all latched faults, is taken from the single plant-wide
  operator reset, and is echoed back to the unit as a physical reset command
  [ref FU-13 + B-15 + B-16 + io `HMIControlSignals.SystemReset`,
  `DiscreteOutputs.CycloneDustFilterReset`]
- **C14** the unit's actual running state is taken from its own running feedback; this class has no
  motion/rotation sensor [ref FU-14 + io `DiscreteInputs.CycloneDustAutoRunning/Stop` — **the sense
  of that signal is unresolved, Q-C05**]
- **C15** running hours are totalised for the unit [ref FU-15]
- **C16** a status indication is published for the operator [ref FU-16]
- **C17** *(alarm indication)* [ref FU-17] → **out of scope for this artifact** (alarms separate).
- **C18** the resulting run command drives the unit's physical start output
  [B-28 + io `DiscreteOutputs.CycloneDustFilterStart`]

## PLANT INTERLOCKS (fully enumerated, one relation per line)

- **P1** no neighbour start permissive — this unit starts on the plant run state alone
  [A rel-9 + io `PlantControl.Status`]. Recorded as a *stated* relation, not as an omission.
- **P2** on controlled shutdown, hold until the plant fans-shutdown-ready condition is present
  [A rel-10, B-06 + io `PlantControl.FansShutdownReady`]
- **P3** on controlled shutdown, hold until the Shredder (`ShredderControlInst1`) reports its
  shutdown complete [A rel-11, B-05]
  → **two lines, not one**: different signals, so §5 forbids the merge. Combination → Q-C09.
- **P4** this unit's enable permits no machine [A rel-12, Q-A7]
- **P5** while the plant is in its running state, this unit is commanded to start and run
  automatically, subject to P2–P3 and C1–C7 [B-02 + io `PlantControl.Status`]
- **P6** while this unit is under hand intervention, its automatic controlled-shutdown request is
  suppressed [B-13]

## SETTINGS

| Setting | Owner | Value | Basis |
|---|---|---|---|
| fan up-to-speed (enable) time | engineering | **10 s** | stated [B-26, REQ-020] — `ProcessTimings.NormalFanStartTime` = 10.0 |
| shutdown-complete time | engineering | **not stated** | Q-C15 |
| fail-to-run / fail-to-stop time | engineering | **not stated** | Q-C15 |
| remote-operation mode | engineering | **not stated** | interface setpoint [ref FU-05]; the *field* remote-operational signal is bound at C5 |

## UNCLAIMED IO (§8(a) per-instance sweep)

| Signal | Disposition |
|---|---|
| `DiscreteInputs.CycloneDustRemOp` | bound — C5 |
| `DiscreteInputs.CycloneDustAutoRunning/Stop` | bound — C14, **sense unresolved Q-C05** |
| `DiscreteInputs.CycloneDustSysOk` | bound — C12 |
| `DiscreteOutputs.CycloneDustFilterStart` | bound — C18 |
| `DiscreteOutputs.CycloneDustFilterReset` | bound — C13 |
| `HMIControlSignals.HandFansShutdown` | **UNCLAIMED — BLOCKING Q-C04** (scoped to the fans, therefore to this unit) |
| `PlantControl.FansShutdownReady` | bound — P2 |

## DELTAS

- **MINUS** — no neighbour start permissive at all; the only in-scope machine with none [A].
- **MINUS** — its enable permits no machine, so class requirement FU-07 has no consumer here
  [A, Q-A7].
- **MINUS** — no machine holds its controlled shutdown on this unit's shutdown-complete.
- **ODD-PAIRING** — its shutdown hold names the Shredder, a material-train machine, while its two
  same-class siblings hold on the Air-Separator VSD [A].
- **PLUS/MINUS (new at this rung)** — it has **no direct fault input**; the fault condition is
  carried by a system-OK signal in the healthy sense [B-18]. Its two siblings have a direct
  `*Flt` input. A same-class, differently-shaped fault source.
- **MINUS (new at this rung)** — no isolation signal exists while the class requires the permissive
  (C2/FU-02) → Q-C03.
- **NEW at this rung** — its running signal's name, `CycloneDustAutoRunning/Stop`, joins two states
  with a `/`. `tagstatus` confirms this is **one** signal that genuinely exists (EXISTS, not
  MEMBER-NOT-FOUND), so nothing was invented — but which state its asserted value denotes is not
  established → Q-C05.
- **NONE-FURTHER-FOUND — searched:** rung-A deltas; class reference §Class requirement set and
  §Class deltas; §8(a) sweep above; §8(b) plant residual sweep; `candidate-scan` IO sets for
  `DiscreteInputs.CycloneDust` (3) and `DiscreteOutputs.CycloneDust` (2); `tagstatus` on all bound
  and attempted names.

## OPEN

- **Q-C03 (BLOCKING)** — no isolation signal for this unit
- **Q-C04 (BLOCKING)** — `HandFansShutdown` unclaimed and in this unit's scope
- **Q-C05 (BLOCKING)** — what does `DiscreteInputs.CycloneDustAutoRunning/Stop` assert?
- **Q-C09 (BLOCKING)** — how P2 and P3 combine
- **Q-A7 (carried, non-blocking)** — this unit's enable permits no machine: is a permissive missing?
- **Q-C15 (non-blocking)** — unstated time values
- **Q-C20 (BLOCKING, plant-level)** — plant-health residual signals
