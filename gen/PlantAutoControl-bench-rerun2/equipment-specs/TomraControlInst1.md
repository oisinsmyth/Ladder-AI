# EQUIPMENT SPEC — Optical Sorter (`TomraControlInst1`)

Rung C (`/gen-equipment-spec`), run `PlantAutoControl-bench-rerun2`, 2026-08-05.
**Signals are bound here. Booleans, interface members and network structure are NOT.**

```
class: optical-sorter (ref v0-derived 2026-08-04)   FB type: TomraControlSystem
fed-by:        NOT ESTABLISHED — no material-flow source (Q-A03)
discharges-to: NOT ESTABLISHED — no material-flow source (Q-A03)
serves:        Sorter Conveyor VSD (by "ready and not faulted", not by the standard enable) [A]
```

**Class caveat carried into this spec:** this class has exactly one instance, so nothing peculiar to
this machine can be distinguished from a class property (Q-A02). Every `[ref OS-nn]` citation below
inherits that weakness.

**Tag verification method:** grep-verified member-by-member; `converter tagstatus`'s pass not relied
on (root-only checking).

## IO BINDING [io]

| Role | Signal | Verified |
|---|---|---|
| hardwired run command → sorter | `DiscreteOutputs.TomraRun` | grep ✓ |
| hardwired ready report | `DiscreteInputs.TomraReady` | grep ✓ |
| hardwired running report | `DiscreteInputs.TomraRunning` | grep ✓ |
| hardwired communications-fault report | `DiscreteInputs.TomraComFlt` | grep ✓ |
| plant run state | `PlantControl.Status` | grep ✓ |
| plant pre-start complete | `PlantControl.PreStartComplete` | grep ✓ |
| plant-wide operator reset | `HMIControlSignals.SystemReset` | grep ✓ |
| data-link process words (8 in / 2 out) | **NO TAGS EXIST** — the source names `Tag_45`…`Tag_54`; **none is present anywhere in `ir/PlantAutoControl-bench/`, and no tag table is exported at all.** Q-C10 | grep ✗ (0 hits) |
| byte-order selector for the data link | **UNRESOLVED** — `PlantControl.Test[…]` is the only candidate; a production selector sitting in a test array. Q-C18 | — |
| inhibit / isolator | **NO SIGNAL EXISTS** — Q-C11 | — |
| hand/auto mode source | **NO SIGNAL EXISTS** — Q-C04 | — |

## CONTROL REQUIREMENTS (complete by construction, OS-01…OS-19)

- **C1** the sorter runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead `[ref OS-01]`
  **CANDIDATES:** `InterlockData.PartInHand` · `HMIControlSignals.GlobalSetAllToAuto` ·
  `TomraControlInst1.Inputs.InHand` `[iface, named-only]` → **BLOCKING Q-C04**
  *Caveat attached: the class reference logs (its anomaly A-2) that as built, a hand start signal
  can only be set while the machine is faulted. That is recorded as an anomaly, not a requirement —
  this line states the class requirement. Q-A17.*
- **C2** the sorter does not start while it is inhibited (isolated / locked off) `[ref OS-02]`
  **CANDIDATES:** *(empty — no `Tomra*IsoFB` exists; fourteen other machines have an isolator
  feedback)* → **BLOCKING Q-C11**
- **C3** the sorter does not start while it has an active fault, and does not start while a
  fail-to-run fault is standing `[ref OS-03]`
- **C4** the sorter does not start until the plant pre-start warning phase has completed
  `[ref OS-04 + io]` — `PlantControl.PreStartComplete`
  *Note: this class has **no** system-healthy permissive, unlike the other two classes in scope —
  the common permissive set is one condition shorter here. Recorded so the difference is visible
  rather than looking like an omission.*
- **C5** the sorter is commanded both over the data link and by a hardwired run signal; both are
  driven together from the same run decision `[ref OS-05 + io]` — hardwired half:
  `DiscreteOutputs.TomraRun`. **Data-link half has no tags — Q-C10.**
- **C6** the sorter's condition is read both over the data link and from hardwired ready / running /
  communications-fault signals `[ref OS-06 + io]` — `DiscreteInputs.TomraReady`, `TomraRunning`,
  `TomraComFlt`. **Data-link half has no tags — Q-C10.**
- **C7** the data link is supervised by a liveness indication from the sorter; if liveness is lost
  for a fixed watchdog period, a communications-lost condition is latched until reset
  `[ref OS-07]` — **no signal (the liveness bit is carried inside the link words of Q-C10); the
  watchdog period is stated as fixed but its value appears in no source this rung may read —
  Q-B11.**
- **C8** while the data link is healthy, the sorter counts as running only when both the link and
  the hardwired running signal say so; while the link is dead, the hardwired running signal alone
  decides. The same fallback applies to the communications-fault indication `[ref OS-08 + io]` —
  hardwired half: `DiscreteInputs.TomraRunning`, `DiscreteInputs.TomraComFlt`.
- **C9** once running for a configurable up-to-speed time, and while the sorter reports itself
  ready, the sorter declares itself enabled, which permits the machine it serves to start; it
  withdraws that enable immediately if it stops running `[ref OS-09 + io]` —
  `DiscreteInputs.TomraReady`
- **C10** on a controlled shutdown request the sorter stops, and declares its shutdown complete once
  a configurable shutdown time has elapsed and it has stopped running `[ref OS-10]`
  *Class difference, recorded: this class has **no** fault or hand escape on shutdown-complete, so a
  faulted sorter does not release the machines waiting on it (Q-A18 / Q-B06).*
- **C11** if the sorter is commanded to run but does not report running within a configurable fault
  time, a fail-to-run fault is raised and latched `[ref OS-11]`
- **C12** if the sorter reports running while not commanded for that same fault time, a fail-to-stop
  fault is raised and latched `[ref OS-12]`
- **C13** any of the sorter's own reported machine faults, plus loss of the data link, raises a
  latched fault `[ref OS-13]` — **the sorter's own reported fault set arrives over the link
  (Q-C10); only the comms-fault path has a hardwired signal.**
- **C14** a fault-reset command clears all latched faults and is also passed to the sorter itself
  over the data link `[ref OS-14 + io]` — `HMIControlSignals.SystemReset`; **link echo has no tags,
  Q-C10.**
- **C15** running hours are totalised for the sorter `[ref OS-15]`
- **C16** a status indication is published for the operator (faulted / stopped / commanded /
  running / enabled) `[ref OS-16]`
- **C17** a wide alarm indication is published, covering the sorter's own reported fault set as well
  as fail-to-run, fail-to-stop and loss of the data link `[ref OS-17]` — *alarms out of scope for
  this control spec; recorded for completeness.*
- **C18** the sorting program to run and the sorter's belt on/off delays are operator-settable
  `[ref OS-18]`
  **CANDIDATES:** *(no HMI signal in the exported IO carries a program selection or a belt delay)*
  `TomraControlInst1.Inputs.ProgramSelection` `[iface, named-only]` ·
  `TomraControlInst1.Inputs.Belt1OnDelay` / `Belt1OffDelay` `[iface, named-only]`
  → **BLOCKING Q-C36** — the operator-facing half of this requirement has no signal at all. The
  class reference records (its anomaly A-3) that these are an interface promise rather than an
  implemented behaviour; whether the *intent* is real is Q-A17/Q-B19.
- **C19** the byte order of the data exchanged with the sorter is selectable at the plant level, to
  suit the machine's actual word ordering `[ref OS-19]`
  **CANDIDATES:** `PlantControl.Test[0..10]` — the only plant-level selector-shaped data in the
  export, and it is **a test/simulation array**
  → **BLOCKING Q-C18** — *a production byte-order selector living in an array named `Test` is either
  a mis-homed setting or a commissioning hack left in; either way it is a control decision taken by
  a test artefact.* Never bound on a guess.

## PLANT INTERLOCKS

- **P1** start permitted only while the Ejected Material Conveyor (`MotorStarterInst6`) is enabled
  `[A + B-08]`
- **P2** start permitted only while the Residual Material Conveyor (`MotorStarterInst7`) is enabled
  `[A + B-08]`
- **P3** on controlled shutdown, hold until the Sorter Conveyor VSD (`MotorVSDInst3`) reports its
  shutdown complete `[A + B-12]`
- **P4** start permitted only while the plant is in its running state `[A + B-02 + io]` —
  `PlantControl.Status`
- **P5** start permitted only after the plant pre-start warning phase has completed
  `[A + B-05 + io]` — `PlantControl.PreStartComplete`
- **P6** the automatic controlled-shutdown request for this machine is suppressed while **hand
  intervention is active** — but *whose* hand intervention is **NOT ESTABLISHED**: the per-machine
  rule `[B-19]` against this machine keying off the **Sorter Conveyor VSD's** hand state
  `[A delta + B-21]`. → **BLOCKING Q-C13.** Neither reading adopted; mirrored on `MotorVSDInst3`.
- **P7** this machine's latched faults are cleared by the single plant-wide operator reset
  `[A + B-24 + io]` — `HMIControlSignals.SystemReset`
- **P8** this machine's ready-and-not-faulted state is the start permissive for the Sorter Conveyor
  VSD (`MotorVSDInst3`) `[A + B-11]` — the plant's only permissive expressed this way rather than by
  the standard enable relation (Q-A15 / Q-C35).
- **P9** the machine's resulting run command drives its physical run output `[B-40 + io]` —
  `DiscreteOutputs.TomraRun` *(same fact as C5's hardwired half; merged, both cited)*
- **P10** a faulted sorter does **not** release the machines held by its shutdown-complete
  `[A O-2 + B-16 + ref]` — recorded as a plant relation because its effect is on other machines
  (the ejected and residual material conveyors, which hold through shutdown until this machine
  reports complete). **Whether this is intended is Q-A18/Q-B06.**

## SETTINGS

| Setting | Owner | Value | Provenance |
|---|---|---|---|
| up-to-speed (enable) time | engineering | **2.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| shutdown time | engineering | **2.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| fail-to-run / fail-to-stop fault time | engineering | **3.0 s** | given instance-DB start value `(boundary)` `[Q-B21]` |
| data-link liveness watchdog period | engineering | **not available to this rung** — stated as fixed by the class reference, value not given in any section this rung may consume | **Q-B11** |
| sorting program selection | HMI | **0** in the given boundary; no operator signal exists | **Q-C36** |
| belt on / off delays | HMI | **0.0 / 0.0** in the given boundary; no operator signals exist | **Q-C36** |
| data byte-order selection | engineering | **unresolved** — only candidate is a test array | **Q-C18** |
| running-hours totaliser interval | engineering | 1 h | given instance-DB start value `(boundary)` |

## UNCLAIMED IO (§8a per-instance sweep)

| Signal | Disposition |
|---|---|
| `DiscreteInputs.TomraReady` | bound — C6, C9; also read by `MotorVSDInst3` P1 |
| `DiscreteInputs.TomraRunning` | bound — C6, C8 |
| `DiscreteInputs.TomraComFlt` | bound — C6, C8; also a candidate for `MotorVSDInst3` P2 (Q-C34) |
| `DiscreteOutputs.TomraRun` | bound — C5, P9 |
| *(no `Tomra*IsoFB`)* | absent — Q-C11 |
| *(no `Tag_45`…`Tag_54`)* | **proposed — the entire data-link word interface is missing from the export. Q-C10.** |
| `PlantControl.Test[0..10]` | **UNCLAIMED, control-implying** — the only candidate for C19's byte-order selection, and a test array. **BLOCKING Q-C18.** |
| `DiscreteInputs.Test[0..75]`, `DiscreteOutputs.Test[0..26]` | **UNCLAIMED, control-implying** — the class reference's anomaly A-1 records a plant test/simulation array overwriting one of this machine's status bits, so a test array is wired into this machine's production path. **BLOCKING Q-C18.** |
| `InterlockData.Simulation` | UNCLAIMED, control-implying — a simulation flag in production interlock data. **Q-C18.** |
| `HMIControlSignals.GlobalSetAllToAuto` | UNCLAIMED, control-implying — **Q-C25** |

## DELTAS

- **Δ1 (Δ) on P6** — shutdown suppression keyed to another machine's hand state. Q-C13.
- **Δ2 (?) on C5/C6/C7/C13/C14/C18** — the data-link half of six class requirements has **no tags in
  the export**, and the source itself does not know whether the word interface is in scope. Q-C10.
- **Δ3 (−) on C2** — no isolator feedback. Q-C11.
- **Δ4 (Δ) on C10/P10** — no fault or hand escape on shutdown-complete, unlike both sibling classes.
- **Δ5 (Δ) on C9** — the enable is withdrawn immediately on loss of running, unlike the sibling
  classes whose enable falls with the timer.
- **Δ6 (−) on C4** — this class has no system-healthy permissive at all; a third distinct treatment
  of machine health across the three classes in scope (Q-B12).
- **Δ7 (Δ) on C19** — the plant-level byte-order selection resolves only to a test array. Q-C18.
- **Δ8 (?) on C1/C18** — two class-reference anomalies (A-2, A-3) bear on requirements stated here;
  neither is inherited as a requirement, both carried as Q-A17.

**DELTAS: none-further-found** — searched: rung-A delta block for this instance, `optical-sorter`
reference §"Class requirement set", §"Class deltas" and §"Observed as-built anomalies", rung-B
behaviours scoped to this instance (B-02, 05, 09, 11, 12, 16, 19, 20, 21, 24, 26, 27, 29, 31, 32,
35, 39, 40, 41, 47, 48, 49, 50, 51, 52), the §8a sweep above, and the §8b plant residual list.

## OPEN

**Blocking:** Q-C04, Q-C10, Q-C11, Q-C13, Q-C18, Q-C25, Q-C36.
**Carried, still blocking:** Q-A01, Q-A02, Q-A03, Q-A13, Q-A16, Q-B01, Q-B09, Q-B18.
**Non-blocking:** Q-A15, Q-A17, Q-A18, Q-B06, Q-B11, Q-B19, Q-B21.
