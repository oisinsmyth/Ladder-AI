# EQUIPMENT SPEC — TomraControlInst1 (Optical Sorter)

    class: optical-sorter (reference v0-derived, 2026-08-04)
    fed-by: not-stated-by-source [A, Q-A4]      discharges-to: not-stated-by-source [A, Q-A4]
    serves: MotorVSDInst3 — by its ready / not-faulted state, not by an enable [A, Q-A6]

**Class caveat that applies to every line below:** this class has exactly one instance, so nothing
peculiar to this machine can be distinguished from a class property (rung A **Q-A3, blocking**).
Read every `[ref OS-nn]` citation as "observed on this one machine", not as "true of the class".

## IO BINDING [io]

| Role | Signal | Status |
|---|---|---|
| hardwired ready | `DiscreteInputs.TomraReady` | EXISTS |
| hardwired running | `DiscreteInputs.TomraRunning` | EXISTS |
| hardwired communications fault | `DiscreteInputs.TomraComFlt` | EXISTS |
| hardwired run command out | `DiscreteOutputs.TomraRun` | EXISTS |
| plant fault-reset command in | `HMIControlSignals.SystemReset` | EXISTS |
| pre-start-complete condition | `PlantControl.PreStartComplete` | EXISTS |
| plant run state | `PlantControl.Status` | EXISTS |
| isolation feedback | **NO SIGNAL** — `DiscreteInputs.TomraIsoFB` → MEMBER-NOT-FOUND | **Q-C03** |
| data-link process words (8 in / 2 out) | **NO TAGS** — `Tag_45` … `Tag_54` → **PROPOSED** | **Q-C14** |
| data-link byte-order selector | `PlantControl.Test[5]` — a **test/simulation array** [ref OS-19] | **Q-C13** |

`converter tagstatus` evidence: `TomraReady` / `TomraRunning` / `TomraComFlt` / `TomraRun` all
EXISTS; `DiscreteInputs.TomraIsoFB` → MEMBER-NOT-FOUND; `Tag_45` → **PROPOSED**, `Tag_54` →
**PROPOSED**. `candidate-scan --scope DiscreteOutputs.Tomra --direction command` returned exactly
**1** IO candidate (`TomraRun`) — the only single-IO-candidate binding in this whole run.

**The three-signal family check.** `candidate-scan --scope DiscreteInputs.Tomra --direction status`
returned 3 same-typed IO signals facing 34 FB members. The three IO names map one-to-one onto three
disjoint hardwired roles (`Ready`, `Running`, `ComError`) that the class reference names
independently at OS-06, so the binding has a documentary basis and is taken. **What is *not* taken
is the further step of deciding which of these — or which of the machine's aggregated fault outputs —
satisfies a *neighbour's* "not faulted" permissive.** That is Q-C12, raised on `MotorVSDInst3` where
the permissive lives.

## CONTROL REQUIREMENTS (complete by construction from the class reference)

- **C1** the sorter runs on the plant automatic start command, unless the operator has taken it to
  hand, in which case it runs on the operator's hand start command instead [ref OS-01]
- **C2** the sorter does not start while it is inhibited (isolated / locked off) [ref OS-02]
  → **no signal carries this for this instance** (`TomraIsoFB` MEMBER-NOT-FOUND). **BLOCKING Q-C03.**
- **C3** the sorter does not start while it has an active fault, and does not start while a
  fail-to-run fault is standing [ref OS-03]
- **C4** the sorter does not start until the plant pre-start warning phase has completed
  [ref OS-04 + io `PlantControl.PreStartComplete`]
- **C5** the sorter is commanded both over the data link and by the hardwired run signal, both
  driven together from the same run decision [ref OS-05 + B-30 + io `DiscreteOutputs.TomraRun`]
  → the data-link half of this requirement has **no tags** (Q-C14).
- **C6** the sorter's condition is read both over the data link and from the hardwired ready,
  running and communications-fault signals [ref OS-06 + B-19 + io `DiscreteInputs.TomraReady`,
  `DiscreteInputs.TomraRunning`, `DiscreteInputs.TomraComFlt`]
- **C7** the data link is supervised by a liveness indication from the sorter; if liveness is lost
  for the watchdog period, a communications-lost condition is latched until reset [ref OS-07]
  → the liveness indication arrives inside the data-link words, which have no tags (Q-C14).
- **C8** while the data link is healthy the sorter counts as running only when both the link and the
  hardwired running signal say so; while the link is dead the hardwired running signal alone
  decides; the same fallback applies to the communications-fault indication
  [ref OS-08 + B-24 + io `DiscreteInputs.TomraRunning`, `DiscreteInputs.TomraComFlt`]
- **C9** once running for the up-to-speed time, and while the sorter reports itself ready, the
  sorter declares itself enabled; it withdraws that enable immediately if it stops running
  [ref OS-09]
  → **note the mismatch with how this machine is actually consumed:** its neighbour's permissive is
  on *ready / not-faulted*, not on this enable [A, Q-A6, and P1/P2 of `MotorVSDInst3`]. The class
  requirement is retained in full; the unused enable is recorded, not deleted.
- **C10** on a controlled shutdown request the sorter stops, and declares its shutdown complete once
  the shutdown time has elapsed and it has stopped running [ref OS-10]
- **C11** if the sorter is commanded to run and does not report running within the fault time, a
  fail-to-run fault is raised and latched [ref OS-11]
- **C12** if the sorter reports running while not commanded for that same fault time, a fail-to-stop
  fault is raised and latched [ref OS-12]
- **C13** any of the sorter's own reported machine faults, plus loss of the data link, raises a
  latched fault [ref OS-13 + B-19]
  → the reported machine faults arrive inside the untagged data-link words (Q-C14).
- **C14** a fault-reset command clears all latched faults and is also passed to the sorter itself
  over the data link [ref OS-14 + B-15 + io `HMIControlSignals.SystemReset`]
- **C15** running hours are totalised for the sorter [ref OS-15]
- **C16** a status indication is published for the operator [ref OS-16]
- **C17** *(a wide alarm indication covering the sorter's reported fault set, fail-to-run,
  fail-to-stop and loss of the data link)* [ref OS-17]
  → **out of scope for this artifact** (alarms are a separate artifact).
- **C18** the sorting program to run and the sorter's belt on/off delays are operator-settable
  [ref OS-18] → the class reference records this as an **interface promise that the as-built does
  not implement** (its anomaly A-3). Retained here as a class requirement with that status stated;
  see SETTINGS and Q-C19. Not silently dropped, and not asserted as working.
- **C19** the byte order of the data exchanged with the sorter is selectable at the plant level, to
  suit the machine's actual word ordering [ref OS-19 + io `PlantControl.Test[5]`]
  → **the selector is an element of a test/simulation array. BLOCKING Q-C13.**

## PLANT INTERLOCKS (fully enumerated, one relation per line)

- **P1** start permitted only while the Ejected Material Conveyor (`MotorStarterInst6`) is enabled
  [A rel-25, B-08]
- **P2** start permitted only while the Residual Material Conveyor (`MotorStarterInst7`) is enabled
  [A rel-26, B-08]
- **P3** on controlled shutdown, hold until the Sorter Conveyor VSD (`MotorVSDInst3`) reports its
  shutdown complete [A rel-27, B-05]
- **P4** the Ejected Material Conveyor holds its controlled shutdown until this machine reports its
  shutdown complete [A rel-29]
- **P5** the Residual Material Conveyor holds its controlled shutdown until this machine reports its
  shutdown complete [A rel-29]
- **P6** this machine's ready state is a permissive for the Sorter Conveyor VSD's start
  [A rel-28, B-10] — the consuming side is `MotorVSDInst3` P1, unresolved there (Q-C12)
- **P7** this machine's not-faulted state is a permissive for the Sorter Conveyor VSD's start
  [A rel-28, B-10] — consuming side `MotorVSDInst3` P2, unresolved there (Q-C12)
- **P8** while the plant is in its running state, this machine is commanded to start and run
  automatically, subject to P1–P3 and C1–C9 [B-02 + io `PlantControl.Status`]
- **P9** **DISPUTED** — while this machine is under hand intervention its automatic
  controlled-shutdown request is suppressed [B-13] **vs** the suppression is gated on **another
  machine's** hand-intervention state [source Q-07, rung B Q-B3]. **BLOCKING Q-C10.**
- **P10** this machine's shutdown-complete has no fault escape, so a fault on it does **not** release
  P4/P5 — the two conveyors holding on it stay held [A delta, ref §Class deltas]. Recorded as a
  plant relation; whether it is intended is Q-A5 (non-blocking).

## SETTINGS

| Setting | Owner | Value | Basis |
|---|---|---|---|
| up-to-speed (enable) time | engineering | **not stated** | Q-C15 |
| shutdown-complete time | engineering | **not stated** | Q-C15 |
| fail-to-run / fail-to-stop time | engineering | **not stated** | Q-C15 |
| data-link watchdog period | engineering | **not stated by the plant sources** | the class reference records a fixed value in the as-built; not restated as a plant setting, not invented |
| sorting program selection | HMI | **not stated** | declared on the interface, not implemented [ref A-3] → Q-C19 |
| belt on-delay / off-delay | HMI | **not stated** | on-delay declared but never computed [ref A-3] → Q-C19 |
| data byte-order selection | engineering | carried on `PlantControl.Test[5]` | **Q-C13** |

## UNCLAIMED IO (§8(a) per-instance sweep)

| Signal | Disposition |
|---|---|
| `DiscreteInputs.TomraReady` | bound — C6 (also a candidate in `MotorVSDInst3` P1) |
| `DiscreteInputs.TomraRunning` | bound — C6, C8 |
| `DiscreteInputs.TomraComFlt` | bound — C6, C8 (also a candidate in `MotorVSDInst3` P2) |
| `DiscreteOutputs.TomraRun` | bound — C5 |
| `PlantControl.Test[5]` | claimed by C19 — **but BLOCKING Q-C13**: a production function bound to a test/simulation array is claimed, not accepted |
| `Tag_45` … `Tag_54` | **NOT BINDABLE — BLOCKING Q-C14.** Named in the source's inventory as the sorter's 8-in/2-out process-data words; `tagstatus` classifies them **PROPOSED**. Hard rule 3: they are recorded as proposed and nothing is coded against them. |

Family completeness: `DiscreteInputs.Tomra*` is exactly {`Ready`, `Running`, `ComFlt`};
`DiscreteOutputs.Tomra*` is exactly {`TomraRun`} — both by candidate-scan enumeration.

## DELTAS

- **PLUS** — exchanges process data over a link in addition to its hardwired signals [A, B-31] —
  and that link has **no tags** (Q-C14), so the plant side of it is unspecified.
- **MINUS (class-level)** — no system-healthy permissive and no remote/local permissive; the
  start-permissive set is one condition shorter than the sibling classes' [A, ref §Class deltas].
- **MINUS (class-level)** — shutdown-complete has no fault or hand escape, so a faulted sorter does
  not release the machines waiting on it [A, ref §Class deltas] → P10, Q-A5.
- **PLUS (class-level)** — its enable is withdrawn immediately on loss of running, unlike the
  sibling classes [A, ref §Class deltas]. Recorded even though nothing consumes the enable (C9).
- **MINUS (new at this rung)** — **no isolation signal exists** while the class requires the
  permissive (C2 / OS-02) → Q-C03. Same finding as all three filter units: four of the six in-scope
  machines inherit an isolation permissive with no signal behind it.
- **NEW at this rung** — a **production** function (data byte order, C19) is carried on an element of
  a **test/simulation** array → Q-C13.
- **NEW at this rung** — C18's operator settings are an interface promise the as-built does not
  implement [ref A-3] → Q-C19.
- **NOT-SEPARABLE** — single-instance class: every "class-level" delta above may be an instance
  delta and vice versa [A, **Q-A3 blocking**]. The delta field for this machine cannot be completed
  by construction, and is marked so rather than being allowed to read as complete.
- **NONE-FURTHER-FOUND — searched:** rung-A deltas; class reference §Class requirement set
  (OS-01..OS-19), §Class deltas and §Observed as-built anomalies (A-1..A-3); §8(a) sweep above;
  §8(b) plant residual sweep; `candidate-scan` on `DiscreteInputs.Tomra` (3 IO) and
  `DiscreteOutputs.Tomra` (1 IO); `tagstatus` on every bound and attempted name including the two
  data-word probes.

### Anomalies inherited from the class reference — recorded, NOT specified

The class reference logs three as-built anomalies inside the given FB. They are **not** written as
requirements anywhere above; they are named here by ID so that no rung below mistakes them for
intent, and so a reviewer can see they were considered: **A-1** (a status bit both read and written
in the same scan, so the fault logic uses last scan's value), **A-2** (a hand start signal that can
only be set while faulted), **A-3** (declared-but-unimplemented operator settings → C18/Q-C19).
Deciding their disposition is out of this rung's scope — they are inside a given, boundary-fixed FB.

## OPEN

- **Q-C03 (BLOCKING)** — no isolation signal for this machine (C2 unsourced)
- **Q-C10 (BLOCKING)** — whose hand-intervention state suppresses this machine's auto shutdown?
- **Q-C13 (BLOCKING)** — a production byte-order selection carried on a test/simulation array element
- **Q-C14 (BLOCKING)** — the sorter's 8-in/2-out data words are PROPOSED, not existing tags
- **Q-C19 (non-blocking)** — C18's operator settings are an unimplemented interface promise
- **Q-C12 (BLOCKING, on `MotorVSDInst3`)** — which of this machine's signals satisfies its
  neighbour's ready / not-faulted permissive
- **Q-A3 (BLOCKING, carried from rung A)** — single-instance class: deltas not separable
- **Q-A5 (non-blocking, carried)** — is a faulted sorter meant to strand the conveyors holding on it?
- **Q-C15 (non-blocking)** — settings with no stated value
- **Q-C20 (BLOCKING, plant-level)** — plant-health residual signals
