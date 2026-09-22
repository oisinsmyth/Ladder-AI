# PlantAutoControl-bench-rerun2 — Block manifest (**PARTIAL — NOT SIGNABLE**)

Emitted by `/gen-code-structure` (rung D) as the handoff view the Build coding stage requires.
`gen-block-new` STOPs without a gate-1-signed `architecture.md`, so this file exists so the project
is *structurally* reviewable — **it is deliberately incomplete and must not be signed.**

> **PARTIAL MANIFEST.** Rung D's own stop condition prevented the D3 render for **all six**
> instances. Every manifest item below therefore states **"renders absent — D3 stopped"** with the
> question IDs responsible, instead of embedding a render. **No render was fabricated to fill this
> section.** The gate block at the end is **NOT SIGNABLE**.

## Provenance

- **Produced:** 2026-08-05, by `gen-code-structure` (rung D), run `PlantAutoControl-bench-rerun2`.
- **Inputs consumed** (git hash of each at write time; `n/a` = produced by this run, uncommitted):

  | Input | Hash |
  |---|---|
  | `gen/PlantAutoControl-bench/requirements.md` (rungs A/B source) | `5cd2c71` |
  | `references/FilterUnitSystem/reference.md`, `vsd-motor`, `optical-sorter` | `8bb2635` |
  | `ir/PlantAutoControl-bench/FilterUnitSystem.ir`, `MotorVSDSystem.ir`, `TomraControlSystem.ir` | `ae0414a` |
  | `ir/PlantAutoControl-bench/Input.ir`, `Output.ir`, `HMIControlSignals.ir`, `Control.ir`, `PLC.ir`, `Timings.ir` | `ae0414a` |
  | `patterns/chained-permissive-enable/pattern.md`, `motor-dol`, `input-mapping`, `output-mapping` | `31f68b5` |
  | `gen/PlantAutoControl-bench-rerun2/equipment-topology.md`, `plant-behaviours.md`, `equipment-specs/*`, `requirements.md`, `unclaimed-signals.md`, `code-structure.md` | n/a (this run) |

- **Designed WITHOUT:** a P&ID or any material-flow drawing (Q-A03) · a supplied functional
  description (Q-B01 — the behaviour source is reverse-derived from as-built code) · an engineering
  standard for any of the three equipment classes (Q-A01 — the references are reverse-derived too) ·
  a tag table export (no tag table exists in `ir/PlantAutoControl-bench/`) · an alarm register (alarms are
  out of a control spec's scope by rung C's calibration).
- **Independence:** rungs A→D were run in sequence in one session; no answer key, grading file or
  post-mortem was consulted — nothing under `docs/evidence/` was opened at any point, and the prior
  runs in `gen/PlantAutoControl-bench/` (beyond the nominated `requirements.md` source) and
  `gen/PlantAutoControl-bench-rerun/` were not read.
- **Safety:** hard rule 2 not triggered; no F-content read, written or referenced.

---

## 1. Block manifest

**Nothing is created.** Every block and DB this design needs already exists. The only new content
would be orchestration networks inside a calling FC — and none could be written.

| Item | Kind | Status | Purpose | Render |
|---|---|---|---|---|
| `FilterUnitSystem` | FB (NUMBER 8) | **existing, untouched** | Filter-unit equipment control (start/stop, enable, shutdown, fault supervision, hours, telemetry, alarms) | n/a — not modified |
| `MotorVSDSystem` | FB (NUMBER 42) | **existing, untouched** | VSD-motor equipment control (as above plus speed reference, direction, ramp allowance) | n/a — not modified |
| `TomraControlSystem` | FB (NUMBER 5) | **existing, untouched** | Optical-sorter control (dual link + hardwired command, link watchdog, wide alarm set) | n/a — not modified |
| `FilterUnitInst2` | instance DB (37) | **existing** | Dust Filter Unit 1 | **renders absent — D3 stopped, see Q-C01, Q-C03, Q-C04, Q-C05, Q-C12** |
| `FilterUnitInst3` | instance DB (40) | **existing** | Dust Filter Unit 2 | **renders absent — D3 stopped, see Q-C02, Q-C03, Q-C04, Q-C05, Q-C12** |
| `FilterUnitInst1` | instance DB (23) | **existing** | Cyclone Filter Unit | **renders absent — D3 stopped, see Q-C03, Q-C04, Q-C05, Q-C06, Q-C12, Q-C19, Q-C20** |
| `MotorVSDInst1` | instance DB (8) | **existing** | Discharge Conveyor VSD | **renders absent — D3 stopped, see Q-C04, Q-C05, Q-C08, Q-C14, Q-C29, Q-C30** |
| `MotorVSDInst3` | instance DB (22) | **existing** | Sorter Conveyor VSD | **renders absent — D3 stopped, see Q-C04, Q-C05, Q-C09, Q-C13, Q-C14, Q-C30, Q-C33, Q-C35** |
| `TomraControlInst1` | instance DB (26) | **existing** | Optical Sorter | **renders absent — D3 stopped, see Q-C10 (structural: no call site exists), Q-C04, Q-C11, Q-C13** |
| *orchestrating FC* | FC | **NOT DETERMINED** | Would hold one network per instance (S1) | **absent — see §"Undetermined items"** |

**C-113 memory test, quoted per sequence** (from the source register's own classification, which
this design adopts unchanged): *"Per-equipment auto-start / stop / interlock permissives — **No**:
each machine's enable/inhibit/start permissive is a function of current plant state, current
neighbour-enable signals, and current feedback; no phase memory is needed in this block"* →
**combinational chained permissives (C-114)**, so the orchestrating unit is an **FC**, not an FB.
The one stateful element the register identifies in this layer (a feed-conveyor reverse latch) is on
a machine outside this run's six.

### Undetermined items

- **The orchestrating FC's identity is not determined by this run** — the six instances are a subset
  of a twenty-machine layer, and naming/creating a new FC for a partial subset would prejudge how the
  remaining fourteen are organised. C-109's area-Main structure needs the whole layer. **Owner
  decision required before Build.**
- **OB100 startup-reset block: NOT DESIGNED, and NOT WAIVED.** §3 records why this is a blocking gap
  rather than an omission.

---

## 2. Interface definitions

The three interface UDTs are the **given, fixed boundary** for this run — interface *design* is not
under test and nothing here proposes to change a member. Members are listed with their role in
`references/<class>/reference.md`; only the design-relevant facts are restated.

### C-115 handshake-vocabulary conformance

| Handshake member | `FilterUnitSystem` (`IO.`) | `MotorVSDSystem` (`IO.`) | `TomraControlSystem` | Deviation |
|---|---|---|---|---|
| enable-in (auto start) | `AutoStartSignal` | `AutoStartSignal` | `Inputs.AutoStartSignal` | conformant |
| enable-out (up-to-speed) | `UPSEnable` | `UPSEnable` | `Outputs.UPSEnable` | conformant |
| running | `RunningFB` | `RunningFwdFB` + `RunningRevFB` | `Outputs.OSRunning` | VSD splits by direction; sorter derives it (link + hardwire, N4) |
| shutdown request / complete | `Shutdown` / `ShutdownComplete` | `Shutdown` / `ShutdownComplete` | `Inputs.Shutdown` / `Outputs.ShutdownComplete` | conformant |
| inhibit | `InhibitMotor` | `InhibitMotor` | `Inputs.InhibitMotor` | conformant |
| pre-start done | `PreStartDone` | `PreStartDone` | `Inputs.PreStartDone` | conformant |
| fault active / reset | `FaultActive` / `FaultReset` | `FaultActive` / `FaultReset` | `Outputs.FaultActive` / `Inputs.FaultReset` | conformant |
| system healthy | `SystemHealthy` | `SystemHealthy` | **absent** | **deviation** — the sorter class has no health permissive at all (a third distinct treatment across three classes; Q-B12) |
| hand selector / intervention | `InHand` / `HandIntervention` | `InHand` / `HandIntervention` | `Inputs.InHand` / `Inputs.HandIntervention` | conformant in shape; **no signal source exists for any of them** (Q-C04) |
| remote/local permissive | `RemoteOp` (**STATIC SETPOINT, not an input**) | absent | absent | **deviation** — FilterUnitSystem only, and not caller-writable (rung D `rebind-3`) |
| grouping struct | one flat `IO` struct | one flat `IO` struct | **three** structs (`HardWireSignals`, `Inputs`, `Outputs`) + 10 bare words | **deviation** — the sorter's caller surface is shaped differently from its siblings (C-607 one-problem-one-policy tension, recorded not fixed) |

### Settings — homing and the C-308 one-writer statement

Every setting the design names is an **instance-DB member** (C-307 per-instance scope: the faceplate
owns it). **The HMI writes it; logic only reads it; there is no scan-copy anywhere in this design.**

| Setting | Owner | Instances and values |
|---|---|---|
| fan start-up / up-to-speed time (`EnableUPSTime`) | engineering | filters 10.0 s (matching `ProcessTimings.NormalFanStartTime`); VSDs 2.0 s; sorter 2.0 s |
| shutdown time (`ShutdownTime`) | engineering | filters 5.0 s; `MotorVSDInst1` 8.0 s; `MotorVSDInst3` 5.0 s; sorter 2.0 s |
| fail-to-run/stop time (`FTTime`) | engineering | `FilterUnitInst2` 2.0 s, `FilterUnitInst3` 3.0 s (Q-C23), `FilterUnitInst1` 60.0 s (Q-C28); VSDs 3.0/5.0 s; sorter 3.0 s |
| VSD ramp allowance (`StartUpTime`) | engineering | 8.0 s / 5.0 s |
| VSD rated max speed (`MaxRPM`) | engineering | 1500 both |
| VSD auto / hand speed | **unresolved (Q-C14)** | instance 100.0 / 30.0 and 100.0 / 20.0, **or** the plant-wide override pair, currently 0.0 |
| filter remote-mode permissive (`RemoteOp`) | engineering | SETPOINT, uninitialised in all three instance DBs |

> **C-308 trap, named so the design makes it impossible.** The §8b sweep found an unclaimed
> plant-wide override feature set (`HMIControlSignals.Global*Overwrite` + `ProcessTimings.Global*`,
> Q-C15) whose naive implementation is a cyclic `MOVE` onto exactly these members. **That
> implementation is forbidden here.** A separate finding (rung D **D-F5**) records that
> `MotorVSDSystem` N6 already performs such a copy *inside the library block*, overwriting the
> HMI-owned hand speed setpoint every scan while in auto — a pre-existing breach this design cannot
> avoid by careful calling.
>
> **C-605:** interface-member comments are an error-severity requirement and **none** of the three
> given UDTs carries member comments. Recorded as an inherited gap on the fixed boundary; not
> changed by this run.

---

## 3. DB landscape

| DB | Role | C-3xx | State |
|---|---|---|---|
| `DiscreteInputs` (15) | local IO input buffer | C-304 | exists |
| `DiscreteOutputs` (16) | local IO output buffer | C-304 | exists |
| `PlantControl` (39) | plant run state, enables, shutdown-ready flags | C-306-adjacent | exists |
| `HMIControlSignals` (38) | operator command surface (`SystemReset`, bypasses, global overrides) | C-306 | exists |
| `ProcessTimings` (3) | plant-wide timings (`NormalFanStartTime` = 10.0) | C-307 | exists |
| `InterlockData` (2) | third-party handshake + plant interlock flags; **carries `Simulation : Bool`** | C-305 | exists |
| instance DBs ×6 | per-equipment interface + internal state | C-302/C-307 | exist |
| `DB_Alarms` | — | C-501 | **absent by design** — alarms are packed per-instance in each FB's own `Alarm` word (C-503), so no category DB is used by these six |
| `DB_Timers` | — | C-407 | **not needed** — every timer is multi-instance inside an equipment FB |
| **OB100 startup-reset block** | — | **C-403 / C-305 / C-124** | **MISSING — BLOCKING (Q-D07, NEW)** |

> **Q-D07 (NEW, blocking) — no startup-reset block exists and no owner waiver is recorded.**
> C-403 requires that *every* bit written by an S/R mechanism appear in a dedicated block executed
> once at PLC startup, **regardless of retentivity**. The three FBs in this design use S/R coils on
> retentive interface members: `IO.StopMotor` (`FilterUnitSystem.ir` N7, `MotorVSDSystem.ir` N8;
> `TomraControlSystem.ir` N12 on `Outputs.Stop`), `IO.HandIntervention` and `IO.HandStartSignal` (N2/N3 in
> both motor FBs, N7/N8 in the sorter), and `RevPosEgde`/`RevNegEge` (`MotorVSDSystem.ir` N9). The `IO`
> structs are declared `RETAIN`. **No OB100 or equivalent appears anywhere in `ir/PlantAutoControl-bench/`
> and no waiver is recorded**, so S/R state survives a power cycle — C-403's own site incident, and
> the hazard C-128 exists to prevent. §"Designed WITHOUT" cannot cover this: C-403 says *no recorded
> waiver, no omission*.
> Note also `InterlockData.Simulation` exists but C-111's simulation gating and C-112's permanent
> HMI banner are not established for this plant; and `DiscreteInputs.Test[0..75]` /
> `DiscreteOutputs.Test[0..26]` are an **IO test override** mechanism, which is a different thing
> from C-111 simulation (Q-C18).

---

## 4. Enable-chain / command-flow graph

Directed **enable** edges (`X → Y` = X's `UPSEnable` is a start permissive for Y). In-scope nodes in
**bold**; others are named neighbours, not specified in this run.

```
MotorStarterInst3 → MotorStarterInst8 → **MotorVSDInst1** → AirStarInst1
                                     ECSControlInst1 ↗
MotorStarterInst6 ┐
MotorStarterInst7 ┴→ **TomraControlInst1** →(ready & not faulted, §note)→ **MotorVSDInst3**
**MotorVSDInst3** → **FilterUnitInst2** ┐
**MotorVSDInst3** → **FilterUnitInst3** ┼→ AirStarInst1  (primary path only)
**MotorVSDInst3** → ECSControlInst1     ┘
**MotorVSDInst1** ────────────────────────→ AirStarInst1  (both paths)
**FilterUnitInst1** → (no successor — Q-C20)
```

**Acyclicity argument.** Walking every enable edge that touches the six: the only path returning
toward a node it left is `MotorVSDInst3 → ECSControlInst1 → MotorVSDInst1 → AirStarInst1`, and
`AirStarInst1` has **no** enable edge back to `MotorVSDInst3` — its only outgoing enable edge goes to
the incline feed conveyor, which does not enable anything in this set. **No cycle.** C-114 satisfied
for the enable graph.

**Interlock/status edges, labelled as such** (C-114's scope note — they may run against flow):

| Edge | Kind |
|---|---|
| `AirStarInst1.ShutdownComplete` → **`FilterUnitInst2`**, **`FilterUnitInst3`**, **`MotorVSDInst1`** | shutdown-hold interlock |
| `ShredderControlInst1.ShutdownComplete` → **`FilterUnitInst1`** | shutdown-hold interlock |
| `ECSControlInst1.ShutdownComplete` → **`MotorVSDInst3`** | shutdown-hold interlock |
| **`MotorVSDInst3`**`.ShutdownComplete` → **`TomraControlInst1`** | shutdown-hold interlock |
| `PlantControl.FansShutdownReady` → the three filter units | plant-level hold condition (**separate from the above — Q-C12, and S3 REJECTED**) |
| **`TomraControlInst1`**`.Outputs.FaultActive` → **`MotorVSDInst3`** | start interlock (rung D `rebind-1`) |

**Direction is provisional.** No `process-topology.md` / P&ID exists, so every edge above comes from
the source register's dependency table, not from material flow (Q-A03). Rung A observation O-1
records three edges that do not read as material flow at all. **No bidirectional equipment** is in
scope, so C-116/C-117 do not apply here.

---

## 5. OB1 call order

**NOT DETERMINED by this run**, and deliberately not guessed: the orchestrating FC for these six is
undetermined (§1), and OB1's order is a whole-layer decision across all twenty machines. What the
design does fix:

- **C-110:** the input map FC is the first call and the output map FC the last; the equipment
  orchestration sits between them. Both map FCs may be called directly from OB1 (C-109's documented
  IO-mapping exception).
- **C-111:** simulation gating positions cannot be stated — no simulation mechanism is established
  for this plant (§3).
- **OB100 call list: cannot be stated — the block does not exist (Q-D07).**

---

## 6. Pattern mapping and freeform share

| Manifest item | Tier | Pattern / base | Note |
|---|---|---|---|
| all three FBs | **(a)** whole library block | `motor-dol` (**ADMITTED**) describes the sibling shape; the three FBs are the real site blocks, reused whole | Extra capability beyond the REQ groups is **accepted, not flagged** (C-606 whole-reuse exception): e.g. the VSD's 11 drive-condition members. |
| the six orchestration networks | **(a)** rung shape | `chained-permissive-enable` (**ADMITTED** 2026-07-15, criterion 3 accepted on partial evidence — its own recorded gap; a genuinely blind instance is still owed) | Not instantiated — D3 stopped. |
| physical IO mapping | **(a)** rung shape, **but the pattern is NOT ADMITTED** | `input-mapping` / `output-mapping` — **proposed, sign-off pending, criterion 3 not started**, and `input-mapping` carries two recorded real data-quality defects (test-index collisions; 38 points missing the override) | **Out of scope for this run** and may not be authored from a non-admitted pattern without the owner's explicit go-ahead, which is not on record. |

**Freeform share: 0%** (tier (d) count 0, basis = planned networks). The >20% freeform flag does not
apply. *This is the one healthy number in the artifact — every piece of logic this design needs
already exists as proven site content; what is missing is the specification to wire it against.*

---

## 7. REQ → block traceability

Trace target: `gen/PlantAutoControl-bench-rerun2/requirements.md` (174 REQs, set-difference 0 against the
spec relations). Full per-relation dispositions are in `code-structure.md` §D2; this table is the
manifest-level view and does not restate it.

| REQ range | Instance | Manifest item | Disposition summary |
|---|---|---|---|
| REQ-001…027 | `FilterUnitInst2` | `FilterUnitSystem` + `FilterUnitInst2` | 9 `in-FB`, 18 `render-BLOCKED`/`out-of-scope-obligation` |
| REQ-028…054 | `FilterUnitInst3` | `FilterUnitSystem` + `FilterUnitInst3` | 9 `in-FB`, 18 blocked/owed |
| REQ-055…081 | `FilterUnitInst1` | `FilterUnitSystem` + `FilterUnitInst1` | 8 `in-FB`, 1 `rebind`, 18 blocked |
| REQ-082…111 | `MotorVSDInst1` | `MotorVSDSystem` + `MotorVSDInst1` | 9 `in-FB`, 20 blocked, 1 owed |
| REQ-112…145 | `MotorVSDInst3` | `MotorVSDSystem` + `MotorVSDInst3` | 9 `in-FB`, 1 `rebind`, 23 blocked, 1 owed |
| REQ-146…174 | `TomraControlInst1` | `TomraControlSystem` + `TomraControlInst1` | 10 `in-FB`, 18 blocked, 1 owed |

**Unmapped REQs: none** — every one of the 174 maps to a manifest item. **But 113 of them map to a
render that does not exist**, which is a different and worse thing than an unmapped REQ, and is why
this manifest is not signable.

**REQ→"no PLC logic, by design":** REQ-017, REQ-044, REQ-071, REQ-101, REQ-131, REQ-162 (the six
per-instance alarm-word relations) are inside their FBs (C-503 per-instance alarm monitoring) and out
of a *control* spec's scope; a separate alarm artifact is owed.

---

## 8. Cross-instance wiring plan (C-127)

**Every cross-instance fact below lives in the orchestrating FC, never inside a reusable FB.**

| Source | Destination | Kind |
|---|---|---|
| `MotorVSDInst3.IO.UPSEnable` | `FilterUnitInst2.IO.AutoStartSignal` (term) | enable |
| `MotorVSDInst3.IO.UPSEnable` | `FilterUnitInst3.IO.AutoStartSignal` (term) | enable |
| `MotorStarterInst8.IO.UPSEnable`, `ECSControlInst1`'s enable | `MotorVSDInst1.IO.AutoStartSignal` (terms) | enable |
| `DiscreteInputs.TomraReady` + `TomraControlInst1.Outputs.FaultActive` | `MotorVSDInst3.IO.AutoStartSignal` (terms) | permissive (`rebind-1`) |
| `MotorStarterInst6`/`Inst7` enables | `MotorVSDInst3` and `TomraControlInst1` `AutoStartSignal` (terms) | enable |
| `AirStarInst1.…ShutdownComplete` | `FilterUnitInst2/3.IO.Shutdown`, `MotorVSDInst1.IO.Shutdown` (negated terms) | interlock |
| `ShredderControlInst1.…ShutdownComplete` | `FilterUnitInst1.IO.Shutdown` (negated term) | interlock |
| `ECSControlInst1.…ShutdownComplete` | `MotorVSDInst3.IO.Shutdown` (negated term) | interlock |
| `MotorVSDInst3.IO.ShutdownComplete` | `TomraControlInst1.Inputs.Shutdown` (negated term) | interlock |
| `PlantControl.FansShutdownReady` | the three filter units' `IO.Shutdown` (term) | plant hold — **kept separate from the neighbour term (Q-C12, S3 REJECTED)** |
| `<Inst>.IO.Run` | `DiscreteOutputs.FilterUnit1Start` / `FilterUnit2Start` / `CycloneDustFilterStart` | output |
| `TomraControlInst1.HardWireSignals.Run` | `DiscreteOutputs.TomraRun` | output (**FB-written; never wire it as an input**) |
| `HMIControlSignals.SystemReset` | all six `FaultReset` members, plus `DiscreteOutputs.FilterUnit1Reset`/`FilterUnit2Reset`/`CycloneDustFilterReset` | fan-out |
| `PlantControl.PreStartComplete` | all six `PreStartDone` members | fan-out |

**C-127 design error caught here, not at review:** `TomraControlSystem` references
`PlantControl.Test[5]` and `PlantControl.Test[7]` **inside its own logic** (N20, N2) — a reusable
equipment FB reaching directly into a plant global DB, and a test array at that. It is inherited
site content, not created by this design, but it is recorded as a design-level defect (**D-F7**,
Q-C18/Q-D05) rather than left for a later reviewer.

---

## 9. Tag status

Corpus: `ir/PlantAutoControl-bench/` at commit `ae0414a`.

**Method note, important.** `converter tagstatus --project ir/PlantAutoControl-bench` was run over the
bound set and returned **31 names, 0 proposed** — and **that result is not the basis of any claim
here.** The tool classifies at **root-DB level only**: it reports
`HMIControlSignals.TotallyInventedMember -> EXISTS` and `HMIControlSignals.BypassOSCRotSen ->
EXISTS`, the latter being a member that does **not** exist. Every classification below was made by
grep against the DB member lists.

| Classification | Names |
|---|---|
| **exists** (root and member both grep-verified) | `DiscreteInputs.FilterUnit1Ready/Op/Flt`, `.FilterUnit2Ready/Op/Flt`, `.CycloneDustRemOp`, `.CycloneDustSysOk`, `.CycloneDustAutoRunning/Stop`, `.AirStarDCRotSen`, `.AirStarDCIsoFB`, `.OSCIsoFB`, `.OSCRotSen`, `.TomraReady`, `.TomraRunning`, `.TomraComFlt`; `DiscreteOutputs.FilterUnit1Start/Reset`, `.FilterUnit2Start/Reset`, `.CycloneDustFilterStart/Reset`, `.TomraRun`; `HMIControlSignals.SystemReset`, `.BypassAirStarDCRotSen`; `PlantControl.Status`, `.PreStartComplete`, `.FansShutdownReady`; `ProcessTimings.NormalFanStartTime` |
| **proposed — named gaps, never coded against (hard rule 3)** | `HMIControlSignals.BypassOSCRotSen` (member absent from an existing DB — the case the tool misreports) · `Tag_45`…`Tag_54`, the sorter's 8-in/2-out link words (**no such root exists anywhere; no tag table is exported**) · a per-machine hand/auto source for all six · an isolator feedback for the three filter units and the sorter · forward/reverse running feedbacks for both VSDs · a drive-error and 11 drive-condition signals for both VSDs · a `RecentStart` source for all six |
| **member-level assumptions, stated explicitly** | every `exists` row above is a *member* claim, verified by grep rather than by the tool; TIA's compile-time check remains the final arbiter |

**A `proposed` result is a recorded gap at this stage, not a run-stopping gate** — but note the
asymmetry: `gen-block-new` halts on PROPOSED, and eight distinct proposed items reach the core of
what these six instances need.

---

## 10. Open questions

**Carried (none silently resolved):** all rung-A blocking questions Q-A01…Q-A16; all rung-B blocking
questions Q-B01…Q-B18; all rung-C blocking questions Q-C01…Q-C37 **except Q-C34**, which rung D's
binding audit closed by the mechanism rung C explicitly deferred to it. Q-B11 was answered as a
verified-in-block fact (5 s link watchdog). Q-C27 was narrowed, not closed.

**NEW at rung D:**

| Q | Summary |
|---|---|
| **Q-D01** | `MotorVSDSystem` fail-to-stop requires **both** running feedbacks simultaneously (N11) — unreachable on a single-direction drive; affects both in-scope VSDs. |
| **Q-D02** | `TomraControlSystem` fail-to-run is gated on `ControlWord0.%X1`, which **nothing writes** (N13) — permanently false. |
| **Q-D03** | `RecentStart` is required by every run latch in all three FBs and driven by nothing in the export; four of the six instances start FALSE and cannot latch a run command. |
| **Q-D04** | `MotorVSDSystem` N6 overwrites the HMI-owned hand speed setpoint every scan (C-308). |
| **Q-D05** | `TomraControlSystem` reads `PlantControl.Test[5]`/`Test[7]` from inside a reusable FB (C-127/C-304 boundary; production behaviour from a test array). |
| **Q-D06** | `VSDWarning`, `OverTempFault`, `ThermalOLFault` default **TRUE** in both VSD instance DBs while undriven. |
| **Q-D07** | **No OB100 startup-reset block exists and no waiver is recorded**, while all three FBs write S/R state onto RETAIN interface members (C-403/C-124/C-128). |

---

## Gate 1 — engineer sign-off

**STATUS: NOT SIGNABLE — PARTIAL MANIFEST.**

This artifact carries every item the design determined and **states, per affected item, that its
render is absent because D3 stopped.** It must not be signed, and the Build coding stage
(`gen-block-new` / the modify pair) **must not start** from it.

Blocking summary: **36 blocking questions** stand (30 rung-C less Q-C34 closed here, plus 7 new at
rung D: Q-D01…Q-D07) on top of 17 carried from rungs A and B · **3 HARD FAILs** in the undriven-input audit · **8 proposed tag
groups** reaching the core of the design · **1 structural blocker** (the optical sorter has no legal
call site) · **1 convention gap with no waiver** (Q-D07).

**Engineer:** — *(unsigned)*
**Date:** — *(none)*
