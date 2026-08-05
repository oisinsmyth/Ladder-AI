# Functional review — PlantAutoControl (PlantAutoControl-bench) (2026-07-20)

Blindness: **blind.** Fresh dispatch; the reviewer did not author or wire the block, and the
conversation carried no author design rationale. Doc-06 rule rationales and the register's own
notes were read (required, non-contaminating); every verdict re-derived from the IR itself. Three
gate-1 design rulings (Q-06/Q-07/net-19 cyclone) were named to the reviewer by the dispatcher as
things to *verify against the REQs* — verification below is derived from the IR + register, not
accepted on assertion.
Register: `gen/PlantAutoControl-bench/requirements.md` @ 5cd2c71
Corpus: `ir/PlantAutoControl-bench/` @ ae0414a ; block `gen/PlantAutoControl-bench/PlantAutoControl.ir` @ 16fae23
Blocks read: `PlantAutoControl` (FC, **generated**). Given boundary read read-only where a REQ is
satisfied inside it: the 8 equipment FB types (`MotorStarter`, `MotorVSDSystem`, `FilterUnitSystem`,
`AirStarSystem`, `EquipmentControlSystem`, `TomraControlSystem`, `MotorFwdRevSystem`,
`ShredderControlSystem`) and the 6 global DBs (`PlantControl`, `HMIControlSignals`, `DiscreteInputs`,
`DiscreteOutputs`, `InterlockData`, `ProcessTimings`) — all **given (imported-real boundary)**, not
under test.

Mechanical assists used: `converter cross-check --project ir/PlantAutoControl-bench/` (reverse-pass
dead-member facts, embedded below), and grep-grounded forward trace over the fully-read 20-network
block. Safety: no network references any F-block, F-runtime, safety program, or E-stop member
(`grep -i estop` on the block returns nothing) — hard rule 2 not triggered.

---

## Per-REQ trace

### REQ-001 — Automatic control of the plant equipment train: **implemented**
Where: networks 1–20 (one per machine).
Evidence: 20 networks, each titled for a distinct machine and each ending in a `CALL <FBtype>(<inst>, EN := TRUE)`; the full equipment inventory of the register (incline feed, link, 2 dust filters, air-sep VSD, discharge VSD, metal-collection, overband magnet, ECS, sorter conveyor VSD, optical sorter, drum sep, ejected/residual conveyors, 2 disc spreaders, feed conveyor, shredder, cyclone, discharge conveyor) is present, in material-train order.
Notes: umbrella REQ; satisfied by the per-machine REQs below.

### REQ-002 — Plant running state commands equipment to start: **implemented**
Where: every network's `AutoStartSignal` coil.
Evidence (N1): `MotorVSDInst2.IO.AutoStartSignal := (PlantControl.Status >= 1 OR ... ) AND AirStarInst1.Outputs.EnableUPS`. The `PlantControl.Status >= 1` running term appears in all 20 auto-start expressions (cross-check: `PlantControl.Status` read in every network N1–N20).
Notes: block reads `Status`, never writes it (correct — Q-01 carried; the sequencer owns it).

### REQ-003 — Sequential (cascaded) controlled shutdown: **implemented**
Where: every network's `AutoStartSignal` shutdown-hold term + `Shutdown` coil.
Evidence: each auto-start holds during `Status = -1` until the named neighbour's `ShutdownComplete`; e.g. N7 `... OR PlantControl.Status = -1 AND NOT MotorStarterInst8.IO.ShutdownComplete ...`. All 20 "holds-until" neighbours were checked against the register's inventory column and **every one matches** (N1→Link+JOB9001, N2→JOB9001, N3/N4/N19→FansShutdownReady, N5→IFC, N6→AirSep, N7→MotorStarterInst81, N8→DischargeVSD, N9→DischargeVSD, N10→ECS, N11→SorterVSD, N12→DrumShutdownReady, N13/N14→Sorter, N15/N16→Residual, N17→DiscSpreaders1&2, N18→FeedConv, N20→Shredder).
Notes: `Shutdown` coil polarity verified safe — asserted only when `Status = -1` AND run-hold released AND not-hand; never asserts while `Status >= 1`. No self-holding-stopped contradiction.

### REQ-004 — Downstream-ready start interlock: **implemented**
Where: every network's `AutoStartSignal` permissive term.
Evidence: each auto-start ANDs the receiving neighbour's `UPSEnable`/`EnableUPS`; all 20 permissives match the register inventory "starts-after" column (e.g. N8 `... AND MotorStarterInst3.IO.UPSEnable`, N14 `... AND MotorStarterInst1.IO.UPSEnable AND MotorStarterInst2.IO.UPSEnable`).

### REQ-005 — Pre-start warning gate: **implemented**
Where: every network's `PreStartDone` coil.
Evidence: all 20 map `...IO/Inputs.PreStartDone := PlantControl.PreStartComplete` (cross-check: `PlantControl.PreStartComplete` read in N1–N20). The gating itself is inside each equipment FB (given boundary); the block's wiring hop — feeding `PreStartComplete` into every FB's `PreStartDone` — is complete.

### REQ-006 — Hand intervention suppresses automatic shutdown: **implemented**
Where: every network's `Shutdown` coil.
Evidence: all 20 gate on **their own** `HandIntervention`, verified machine-by-machine (N1 `NOT MotorVSDInst2.IO.HandIntervention` … N20 `NOT MotorStarterInst9.IO.HandIntervention`).
Notes: **Q-07 verified** — the as-built cross-reference quirk (nets 10/11 gating on a *different* machine's hand flag) is **not reproduced**; N10 uses `MotorVSDInst3.IO.HandIntervention`, N11 uses `TomraControlInst1.Inputs.HandIntervention`. The gate-1 ruling to implement own-hand uniformly is faithful to REQ-006's stated intent; Q-07 remains carried (owner ruling on the answer-key quirk still open, but the register writes REQ-006 as the requirement and the block meets it).

### REQ-007 — Single global fault reset: **implemented**
Where: every network's `FaultReset` coil.
Evidence: all 20 map `...FaultReset := HMIControlSignals.SystemReset` (cross-check: `SystemReset` read in N1–N20). Physical reset echoes: N3 `DiscreteOutputs.FilterUnit1Reset := HMIControlSignals.SystemReset`, N4 `FilterUnit2Reset`, N19 `CycloneDustFilterReset` (REQ-019); N9 drives `ECSReset` from the FB reset output (REQ-018).

### REQ-008 — Computed run command drives the physical output: **implemented**
Where: N2,3,4,7,8,9,11,12,13,14,15,16,17,19,20 (the discrete-output machines).
Evidence: e.g. N2 `DiscreteOutputs.HFLCStart := MotorStarterInst4.IO.Run`; N9 `ECSAutoStart := ECSControlInst1.Outputs.ECSRun`; N11 `TomraRun := TomraControlInst1.HardWireSignals.Run`; N17 `SFCRunFwd := ...RunFwd`, `SFCRunRev := ...RunRev`. The five bus/VSD machines with no discrete start bit (N1 incline VSD, N5 air-sep VSD, N6 discharge VSD, N10 sorter VSD, N18 shredder) correctly drive only through the FB interface — consistent with the register note and the absence of matching `DiscreteOutputs` members.

### REQ-009 — Running confirmation from field feedback: **implemented**
Where: N2,3,4,7,8,12,13,14,15,16,17,20 (register source list) + VSD/comms nets.
Evidence: each maps a `RunningFB`/`RunningFwdFB` from a field input, e.g. N8 `RunningFB := DiscreteInputs.OverbandMagRunning`; N3/N4 use `FilterUnit1/2Ready`; N5 `ComsInputs.MachineRunning := DiscreteInputs.AirStarRunning`.

### REQ-010 — Isolator inhibits the motor: **implemented**
Where: N1,2,6,7,8,10,12,13,14,15,16,17,20 (register source list).
Evidence: each maps `InhibitMotor := DiscreteInputs.*IsoFB`, e.g. N10 `InhibitMotor := DiscreteInputs.OSCIsoFB`. All 13 source networks present; the 7 non-isolator machines (filters, air-sep, ECS, sorter, shredder, cyclone) correctly omit it.

### REQ-011 — Belt-conveyor motion confirmation, operator-bypassable per conveyor: **implemented**
Where: N2,7,13,14,17,20.
Evidence: each uses `Running AND RotSen OR Running AND Bypass<own>RotSen`, e.g. N13 `RunningFB := DiscreteInputs.OSECRunning AND DiscreteInputs.OSECRotSen OR DiscreteInputs.OSECRunning AND HMIControlSignals.BypassOSECRotSen`. Each conveyor references **its own** bypass flag (verified N2/HFLC, N7/FMCC, N13/OSEC, N14/OSRC, N17/SFC, N20/SDC).

### REQ-012 — Discharge-conveyor VSD running feedback derived from its rotation sensor: **partial**
Where: N6.
Evidence: `COIL MotorVSDInst1.IO.RunningFwdFB := DiscreteInputs.AirStarDCRotSen` — the "motion sensed = running" mapping is present.
Notes: the REQ's **"unless that sensor is bypassed, in which case it is not used"** clause is **not implemented** — the mapping is unconditional; `HMIControlSignals.BypassAirStarDCRotSen` is never referenced (cross-check: `BypassAirStarDCRotSen [global-db]: unused (no writer, no reader)`; the DB member exists, HMIControlSignals.ir line 29). The block comment on N6 admits "bypass handling deferred - review." Missing part: the bypass gate on the discharge-VSD running source.

### REQ-013 — Sub-system enable gates: **implemented**
Where: N7 (MagEnable), N12 (DrumsEnable), N13/N20/N17 (GeneralEnable).
Evidence: N7 `... AND PlantControl.MagEnable`; N12 `... AND PlantControl.DrumsEnable`; N13/N20 `... AND PlantControl.GeneralEnable`; N17 reverse path on `PlantControl.GeneralEnable`. Matches the register source list (7,12,13,20,17) and inventory extra-gate column. The REQ text phrase "residual/discharge conveyors" is loose; the authoritative inventory table and source citation resolve it to N13/N20/N17 (N14 residual is gated by disc-spreader UPSEnable, per its inventory row) — no defect.

### REQ-014 — Third-party / upstream-plant interlock on incline feed & link conveyor: **implemented**
Where: N1, N2.
Evidence: N1 holds through shutdown on `NOT InterlockData.JOB9001ShutdownComplete` (shutdown-state only, per inventory "JOB9001 interlock (shutdown)"). N2 gates start on `... AND InterlockData.JOB9001Interlock` and holds on `NOT InterlockData.JOB9001ShutdownComplete` (inventory "both states"). Both `JOB9001` members exist (PLC.ir).

### REQ-015 — Automatic pre-start request: **implemented**
Where: N2.
Evidence: `SCOIL PlantControl.AutoPreStart := MotorStarterInst4.IO.AutoStartSignal AND NOT PlantControl.PositiveEdgeArray[4]` / `RCOIL PlantControl.AutoPreStart := DiscreteOutputs.RunPreStart` / `COIL PlantControl.PositiveEdgeArray[4] := MotorStarterInst4.IO.AutoStartSignal`. Correct rising-edge one-shot (set fires only on the scan where auto-start is true and the edge-memory is still false; edge-memory then latches, blocking re-set), cleared when the pre-start output energises. `PositiveEdgeArray[4]` is in bounds (`Array[0..7]`). Keyed off the link conveyor (Q-04 carried — placement inferred, logic correct).

### REQ-016 — Shredder feed conveyor reverses to control feed: **partial**
Where: N17.
Evidence: `COIL MotorFwdRevInst1.IO.Reverse := NOT ShredderControlInst1.Outputs.UPSEnable AND PlantControl.GeneralEnable` — direction selection (reverse when shredder not ready, forward when ready) is correct; auto-start covers both fwd (`... AND ShredderControlInst1.Outputs.UPSEnable`) and rev (`... AND PlantControl.GeneralEnable`) paths.
Notes: two gaps against the REQ/register. (1) The register's C-113 table classifies this as **latched state** ("reverse is latched on the shredder-not-ready condition and released on shredder-ready … the one small stateful element in this block"); the block implements it as a **bare combinational** `Reverse` coil with no in-block latch — a C-113/C-118 paradigm mismatch (see reverse-pass sequence-paradigm check). (2) The REQ clause "the direction only changes while the conveyor is already running automatically and is not under hand control" is not gated on the `Reverse` coil in this block; any such qualification is delegated to `MotorFwdRevSystem` (its `IO.HandReverse` member exists but is unused; the stop-before-reverse pause is stated to live in the FB, C-117). Direction correctness confirmed; the hold/latch and running/not-hand qualifiers are not verifiable in the reviewable scope → S9 sim candidate.

### REQ-017 — Optical sorter integration and readiness interlock: **implemented**
Where: N11 (sorter), N10 (sorter-conveyor VSD).
Evidence: N11 brings in the three hardwired signals (`HardWireSignals.Ready := DiscreteInputs.TomraReady`, `.Running := TomraRunning`, `.ComError := TomraComFlt`) and drives `DiscreteOutputs.TomraRun := TomraControlInst1.HardWireSignals.Run`. N10 permissive: `... AND DiscreteInputs.TomraReady AND ... AND NOT DiscreteInputs.TomraComFlt` — conveyor allowed only while sorter ready and not faulted.
Notes: **Q-06 verified** — the sorter comms data words (`Tag_45…Tag_54`) are deliberately not wired. REQ-017's text scopes the requirement to the hardwired signals + run command + conveyor permissive; the comms words appear only in the register's Q-06 note as a "given comms buffer, not a derivable requirement." Not wiring them is consistent with the register; Q-06 remains carried.

### REQ-018 — Equipment Control System (ECS) integration: **implemented**
Where: N9.
Evidence: brings in all ECS field signals for which the `EquipmentControlSystem` FB has an input — `RunningFB, ReadyFB, MSHandMode, MSAutoMode, MSSettingMode, SumFlt, FCRotorFault, FltRotorRevs, FCConveyorBeltFlt, InterlockingFeeder, MaterialChargeEnabled` (fault/mode/ready/running/rotor/conveyor/feeder/charge categories all represented), and drives `DiscreteOutputs.ECSAutoStart := ...Outputs.ECSRun` and `ECSReset := ...Outputs.ECSReset`.
Notes: the DB spare input `DiscreteInputs.ECSFltConveyotRevs` is not mapped — verified the ECS FB interface (EquipmentControlSystem.ir) has **no** corresponding conveyor-revs input, so there is nowhere to wire it; not a gap. Participates in the cascade like the others (REQ-002/003/004 satisfied in N9).

### REQ-019 — Filter-unit feedback mapping and reset: **partial**
Where: N3, N4 (dust filters — complete), N19 (cyclone — incomplete).
Evidence (complete): N3 `FilterUnitInst2.RemoteOp := DiscreteInputs.FilterUnit1Op`, `IO.RunningFB := FilterUnit1Ready`, `IO.FaultFB := FilterUnit1Flt`, `DiscreteOutputs.FilterUnit1Reset := SystemReset`; N4 identical for unit 2. N19 cyclone: `RemoteOp := CycloneDustRemOp`, `IO.FaultFB := NOT DiscreteInputs.CycloneDustSysOk` (fault = inverse of system-OK, as specified), `CycloneDustFilterReset := SystemReset`.
Notes: the cyclone's **running feedback is not wired** — `FilterUnitInst1.IO.RunningFB` has no assignment in N19 (N3/N4 map it from `Ready`). Root cause is a **given-boundary DB defect**: the intended source `DiscreteInputs.CycloneDustAutoRunning/Stop` (Input.ir line 97) contains a literal `/`, an illegal identifier character (C-005) that is un-referenceable in IR; cross-check confirms `CycloneDustAutoRunning/Stop [global-db]: unused (no writer, no reader)`. The block comment on N19 documents this. This is not a fault in the candidate's logic but a defect in the fixed boundary; functionally, the cyclone FB runs without a running confirmation until the DB member is renamed. See "Suspected functional defects" and NEW question below.

### REQ-020 — Filter-unit fan start-up time (10 s): **implemented**
Where: N3, N4, N19.
Evidence: each has `MOVE(EN := TRUE, IN := ProcessTimings.NormalFanStartTime) => FilterUnitInstX.IO.EnableUPSTime`. `ProcessTimings.NormalFanStartTime` start value = **10.0** (Timings.ir line 7), matching the register's commissioning value. The seconds→ms conversion is inside `FilterUnitSystem` (given boundary; cross-check shows `FilterUnitSystem N6` derives `EnableUPSTimeMS`) — this block correctly passes the seconds setpoint. Number verified against the DB start value; no mismatch.

### REQ-021 — Air-separator VSD alternative start permissive: **implemented**
Where: N5.
Evidence: `AutoStartSignal := (...) AND MotorVSDInst1.IO.UPSEnable AND FilterUnitInst2.IO.UPSEnable AND FilterUnitInst3.IO.UPSEnable OR (...) AND MotorVSDInst1.IO.UPSEnable AND InterlockData.LinkOut3rdParty` — the two OR'd paths exactly match REQ-021 (discharge VSD + both dust filters, OR discharge VSD + third-party link-out). `InterlockData.LinkOut3rdParty` exists, start value TRUE (PLC.ir line 14). Q-08 carried (process meaning inferred; logic faithful).

### REQ-022 — Health / safety supervised outside this layer: **out-of-scope (confirmed)**
Where: N1–N20.
Evidence: the block ties `SystemHealthy := TRUE` for every machine whose FB interface has that input (MotorStarter/MotorVSDSystem/FilterUnitSystem/AirStar instances). N9 (ECS), N11 (sorter), N18 (shredder) have no such assignment — verified those three FBs (EquipmentControlSystem.ir, TomraControlSystem.ir, ShredderControlSystem.ir) declare **no** `SystemHealthy`/`Healthy` input, so there is nothing to tie. No E-stop, plant-health, or safety-interlock logic exists in the block; the E-stop feedback members in the given DBs are untouched. No PLC logic pretends to own the hardwired safety function.

---

## Unimplemented / disarmed / contradicted REQs
- **None.** No REQ is unimplemented, disarmed, or contradicted. The `Shutdown`/`AutoStartSignal`
  polarity was specifically checked (per the skill's stop-polarity precedent) and is correct in all
  states. Three REQs are **partial**:
  - **REQ-012** — rotation-sensor-as-running mapping present, but the "unless bypassed" clause is
    missing (`BypassAirStarDCRotSen` never referenced). Block-authored gap.
  - **REQ-016** — fwd/rev direction selection correct, but the register's latched-state (C-113)
    classification is not realized in-block and the "only while running / not hand" qualifier is
    delegated to the FB. Paradigm/structure gap; may be covered by the FB (S9).
  - **REQ-019** — dust filters complete; the **cyclone running feedback is unwired** because its
    source DB member name contains an illegal `/` (given-boundary defect, not the candidate's fault).

## Unrequested logic (reverse pass)
- **None.** Every network's function maps to a REQ. The only global-DB writes the block authors
  beyond the equipment interfaces are `PlantControl.AutoPreStart` (set/reset) and
  `PlantControl.PositiveEdgeArray[4]` — both serving REQ-015. No unrequested alarms, outputs, or
  features; no C-606 justification lines needed because there is no unrequested logic.
- Sequence-paradigm check (C-113): 19 of 20 machines are combinational chained-permissive interlocks,
  matching the register's classification. **REQ-016 (N17) is the one mismatch** — register classifies
  it *latched state*, block implements it *combinational* (relies on `MotorFwdRevSystem` for any
  memory). Cited C-113/C-118 as a functional-structure finding (see REQ-016). REQ-015's one-shot
  matches its "edge-triggered one-shot" classification.

## Suspected functional defects outside any REQ
- **Given-boundary DB defect (blocks REQ-019 cyclone running feedback):**
  `DiscreteInputs.CycloneDustAutoRunning/Stop` (Input.ir line 97) contains a literal `/` — an
  illegal identifier character (C-005) that cannot be referenced from IR. The consequence is the
  cyclone filter's running feedback cannot be wired. This is a defect in the fixed boundary the
  Candidate was handed, not in the generated block; the correct fix is the engineer renaming the DB
  member (e.g. `CycloneDustAutoRunningStop`), after which N19 can map `FilterUnitInst1.IO.RunningFB`.
  cross-check confirms the member has no reader and no writer anywhere.
- No block-authored dead wiring, multi-writer conflict, or self-fighting write. The single
  multi-writer path cross-check flags (`PlantControl.AutoPreStart`: N2 set + N2 reset) is the
  intended REQ-015 S/R one-shot, not a conflict. `PlantControl.AutoPreStart` shows
  "written-but-never-consumed" only because its consumer (the pre-start timer/sequencer) is an
  out-of-scope block; likewise `DiscreteOutputs.RunPreStart` reads as "consumed-but-never-written"
  because the pre-start block drives it — both expected across the layer boundary, not defects.

## Open questions (carried + newly raised)
- **Q-01** (Status encoding) — carried. Block reads `Status >= 1` (run) / `= -1` (shutdown), never
  writes it. Correct per register interpretation.
- **Q-02** (material-flow direction) — carried. Permissive/hold graph matches the inventory; only
  the "downstream" rationale is inferred.
- **Q-03** (JOB9001 meaning) — carried. Wiring faithful (REQ-014); semantics inferred.
- **Q-04** (auto-pre-start trigger point) — carried. Keyed off link conveyor per register; placement
  inferred.
- **Q-05** (feed-conveyor reversal purpose) — carried; relevant to REQ-016. Direction logic faithful.
- **Q-06** (sorter data words) — carried and **verified consistent**: words deliberately unwired,
  outside REQ-017's text scope.
- **Q-07** (cross-referenced hand flags) — carried and **verified**: the block does not reproduce the
  as-built quirk; all 20 nets gate on their own hand flag (REQ-006). Owner ruling on the answer-key
  quirk still open, but the block matches the requirement as written.
- **Q-08** (air-sep alternative permissive) — carried. Logic faithful (REQ-021); process meaning
  inferred.
- **NEW-01** — The cyclone running-feedback gap (REQ-019) is forced by the illegal `/` in
  `DiscreteInputs.CycloneDustAutoRunning/Stop`. Owner decision needed: rename the DB member so the
  feedback can be wired, or confirm the cyclone is intended to run without a running confirmation.
- **NEW-02** — REQ-012 discharge-VSD rotation-sensor bypass (`BypassAirStarDCRotSen`) is unimplemented
  and the N6 comment marks it "deferred." Owner/engineer: is the bypass required for this VSD (as it
  is for the belt conveyors, REQ-011), or is the unconditional rotation-sensor-as-running acceptable
  here?
- **NEW-03** — REQ-016 feed-conveyor reverse is combinational in this block whereas the register's
  C-113 table expected a latched state. Confirm whether `MotorFwdRevSystem` holds the reverse
  latch/qualifiers (making the combinational feed correct) or whether the latch belongs in this block.

## Not statically checkable (S9 sim-test candidates, keyed by REQ ID)
- **REQ-016** — exercise a shredder-readiness transition while the feed conveyor is running vs stopped,
  and in hand mode, to confirm `MotorFwdRevSystem` supplies the "direction only changes while running
  and not in hand" hold that this block delegates to it, and that the combinational `Reverse` feed does
  not chatter through the FB's stop-before-reverse pause.

---

## Headline
The block **implements the spec.** 18 of 22 REQs fully implemented, 0 contradicted, 0 disarmed, 0
unimplemented, and **no gold-plating** (clean reverse pass). Cascade start-permissives and
shutdown-hold neighbours were verified machine-by-machine and all 20 match the register inventory;
shutdown polarity is correct. The three partials are well-contained and honestly documented in-block:
REQ-019 (cyclone running feedback) is blocked by an illegal `/` in a **given** DB member — not the
candidate's fault; REQ-012 (discharge-VSD bypass) is a genuine block-authored missing clause; REQ-016
(feed reverse) is a C-113 paradigm mismatch that may be covered by the given FB. Two gate-1 rulings
verified sound against the REQs (Q-06 comms words out of scope; Q-07 own-hand uniform, quirk not
reproduced). Recommend the engineer close NEW-01 (rename the DB member) and rule on NEW-02/NEW-03
before the block is considered complete.

### Per-class tally
| Class | REQs | implemented | partial | unimplemented | contradicted | out-of-scope |
|---|---|---|---|---|---|---|
| control | 16 | 13 | 3 (012, 016, 019) | 0 | 0 | — |
| mode | 2 | 2 | 0 | 0 | 0 | — |
| HMI | 2 | 2 | 0 | 0 | 0 | — |
| timing | 1 | 1 | 0 | 0 | 0 | — |
| out-of-scope | 1 | — | — | — | — | 1 (022) |
| **total** | **22** | **18** | **3** | **0** | **0** | **1** |
