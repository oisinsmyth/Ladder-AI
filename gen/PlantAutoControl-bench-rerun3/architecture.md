# PlantAutoControl-bench-rerun3 — Block manifest (PARTIAL)

> ## ⚠ THIS MANIFEST IS PARTIAL AND NOT SIGNABLE
> Rung D's render stage (D3) **stopped** on its own stop condition. **No manifest item carries a
> render**, because none exists. Every affected item states *"renders absent — D3 stopped"* with the
> question IDs responsible. `gen-block-new` must **not** start from this file.
> A complete manifest is emitted only when the blocking set is answered and D3 is re-run.

## Provenance

- **Produced:** 2026-08-05, `gen-code-structure` (rung D output section), inside `lad-coder`.
- **Inputs consumed, with git hashes:**

| Input | Hash | Role |
|---|---|---|
| `gen/PlantAutoControl-bench-rerun3/equipment-specs/*.md` (6) | uncommitted (this run) | the control specs — required input |
| `gen/PlantAutoControl-bench-rerun3/requirements.md` | uncommitted (this run) | REQ trace target for section 7 |
| `gen/PlantAutoControl-bench-rerun3/code-structure.md` | uncommitted (this run) | D0 shapes, D1 fit, D2 ledger |
| `gen/PlantAutoControl-bench-rerun3/unclaimed-signals.md` | uncommitted (this run) | plant residual sweep |
| `gen/PlantAutoControl-bench/requirements.md` | `5cd2c71` | rung A topology source + rung B behaviour source |
| `references/{FilterUnitSystem,vsd-motor,optical-sorter}/reference.md` | `8bb2635` | class requirement sets |
| `ir/PlantAutoControl-bench/` (34 files) | `3661b98` / `ae0414a` (sampled) | IO table, FB types, instance DBs |
| `patterns/chained-permissive-enable/pattern.md` | `31f68b5` | shape vocabulary (ADMITTED) |
| `docs/06-lad-conventions.md` | `1e90300` | read fresh this run |

- **Designed WITHOUT:** a P&ID (`process-topology.md` does not exist — rung A's `fed-by` /
  `discharges-to` are `not-stated-by-source`, Q-A4); an independent functional description (the
  behaviour source is reverse-derived from the as-built, Q-B1); a reviewed engineering standard for
  any equipment class (the references are code-derived, Q-A2).
- **Scope:** six instances. The plant has twenty; the other fourteen are referenced by interlocks and
  are **not** designed here.
- **Safety:** hard rule 2 respected throughout. E-stop / safety-circuit signals present in the
  ordinary global DBs are recorded as an untouched boundary in `unclaimed-signals.md` §3 and appear
  in no manifest item.

---

## 1. Block manifest

**No block is created by this design.** Every block it touches already exists as a given library
block or given instance DB. The one block this design *would* create — the orchestrating control FC
that wires the six instances — **cannot be specified**, because D3 produced no render for any of its
networks.

| # | Item | Kind | Purpose | State |
|---|---|---|---|---|
| M1 | `FC_ControlMain`-role orchestrating FC (name not fixed — no existing block in scope claims this role for these six machines) | FC | One network per equipment instance: wire each instance's interface from plant state, neighbour enables and field signals, then `CALL` its FB. C-113 memory test = **No** (D0 shape S2), so an FC is correct — no phase memory is held here. | **renders absent — D3 stopped**, see Q-C01…Q-C14, Q-C16, Q-C20, Q-C21 |
| M2 | `FilterUnitSystem` (block 8) | FB, **existing, unmodified** | Filter-unit class control. Reused whole (C-108/C-606). | unchanged |
| M3 | `MotorVSDSystem` (block 42) | FB, **existing, unmodified** | VSD-motor class control. Reused whole. | unchanged |
| M4 | `TomraControlSystem` (block 5) | FB, **existing, unmodified** | Optical-sorter class control. Reused whole. | unchanged |
| M5 | `FilterUnitInst2` (DB 37) | instance DB, existing | Dust Filter Unit 1 | wired by M1 — **renders absent**, Q-C01, Q-C03, Q-C04, Q-C09, Q-C20 |
| M6 | `FilterUnitInst3` (DB 40) | instance DB, existing | Dust Filter Unit 2 | wired by M1 — **renders absent**, Q-C02, Q-C03, Q-C04, Q-C09, Q-C20 |
| M7 | `FilterUnitInst1` (DB 23) | instance DB, existing | Cyclone Filter Unit | wired by M1 — **renders absent**, Q-C03, Q-C04, Q-C05, Q-C09, Q-C20 |
| M8 | `MotorVSDInst1` (DB 8) | instance DB, existing | Discharge Conveyor VSD | wired by M1 — **renders absent**, Q-C08, Q-C11, Q-C16, Q-C20, Q-C21 |
| M9 | `MotorVSDInst3` (DB 22) | instance DB, existing | Sorter Conveyor VSD | wired by M1 — **renders absent**, Q-C06, Q-C07, Q-C10, Q-C12, Q-C16, Q-C20 |
| M10 | `TomraControlInst1` (DB 26) | instance DB, existing | Optical Sorter | wired by M1 — **renders absent**, Q-C03, Q-C10, Q-C12, Q-C13, Q-C14, Q-C19 |

**Instance-DB naming:** the existing names (`FilterUnitInst2`, …) are the project's frozen
identifiers (C-004) and are used verbatim. They do **not** follow C-003's `iDB_<FBName>_<Instance>`
form — a pre-existing project convention, not a decision of this design, and not renamed (renaming a
frozen equipment identifier is exactly what C-004 forbids).

**Naming trap carried into the manifest:** `FilterUnitInst2` is Dust Filter Unit **1** and
`FilterUnitInst3` is Dust Filter Unit **2**. The signal families are `FilterUnit1*` and
`FilterUnit2*` respectively. Any coder reading this manifest must not "correct" the off-by-one.

## 2. Interface definitions

The three interface UDTs (`MotorIOSet`, `MotorVSDIOSet`, and the sorter's `Inputs`/`Outputs`/
`HardWireSignals` structs) are the **given Phase-2 boundary**. This design does **not** change any
interface member, add one, or re-home one. Full member lists are in the FB IR, not restated here
(section 7 points into it rather than duplicating).

**C-115 handshake-vocabulary conformance:**

| Member | `FilterUnitSystem` | `MotorVSDSystem` | `TomraControlSystem` | Note |
|---|---|---|---|---|
| `AutoStartSignal` | yes | yes | yes (`Inputs.`) | — |
| `UPSEnable` (enable out) | yes | yes | yes (`Outputs.`) | — |
| `RunningFB` | yes | `RunningFwdFB`/`RunningRevFB` | `Outputs.OSRunning` | deviation: VSD splits by direction; sorter computes it |
| `Shutdown` / `ShutdownComplete` | yes | yes | yes | — |
| `InhibitMotor` | yes | yes | yes | — |
| `SystemHealthy` | yes | yes | **absent** | class deviation, recorded at rung A |
| `HandIntervention` | yes | yes | yes | — |
| `FaultReset` | yes | yes | yes | — |
| `RemoteOp` | yes (setpoint) | **absent** | **absent** | FilterUnitSystem-only permissive (FU-05) |
| `RotationSensor` | **absent** | yes — **DEAD-INTERFACE on both instances** | **absent** | BA-1/BA-2 |

**Settings, C-307/C-308:** every setting these six machines use is **instance-owned** and lives in
the instance DB (`FTTime`, `EnableUPSTime`, `ShutdownTime`, `StartUpTime`, speed setpoints,
`RemoteOp`). **One writer: the HMI.** This design writes none of them and **scan-copies none of
them** — explicitly including `ProcessTimings.Global*`, for which an unspecified plant-wide overwrite
mechanism exists (Q-C23). Making that copy impossible, rather than merely not writing it, is the
requirement; the orchestration networks contain no `MOVE` onto any settings member and the D2 ledger
has no row that would produce one.

## 3. DB landscape

| DB | Role | Convention | State |
|---|---|---|---|
| `DiscreteInputs` (15) | input buffer, local IO | C-304 buffer pair | existing, unchanged |
| `DiscreteOutputs` (16) | output buffer, local IO | C-304 buffer pair | existing, unchanged |
| `HMIControlSignals` (38) | operator command surface | C-306 `DB_Controls` role | existing, unchanged |
| `PlantControl` (39) | plant run state + enables | C-305 `DB_PLC` role — **but see below** | existing, unchanged |
| `InterlockData` (2) | third-party + plant-wide interlock flags | no direct C-30x analogue | existing, unchanged |
| `ProcessTimings` (3) | plant-wide timings | C-307 `DB_Settings` role | existing, unchanged |
| **`DB_Alarms`** | — | C-501 | **absent from the export.** Alarms are out of scope for this pipeline run by rung C's calibration; the absence is recorded, not designed around. |
| **`DB_Timers`** | — | C-407 | not required: every timer in scope is multi-instance inside an equipment FB, which is what C-407 asks for. |
| **OB100 / startup reset** | — | C-305 / C-403 / C-124 | **NOT PRESENT in this design, and NOT waived.** See the finding below. |

**Finding — C-305/C-403/C-124 startup machinery.** `PlantControl` fills the `DB_PLC` role but has
**no `Simulation : Bool`** member (C-305 requires it); the export's simulation-looking flag is
`InterlockData.Simulation`, which is in a different DB and is claimed by nothing (Q-C25). No OB100 or
equivalent startup-reset block appears in the export or in this design's scope. Per section 3's own
rule — *present in every design, or explicitly waived with a recorded owner-waiver citation* — and
with **no recorded waiver**, this is a named gap, not an omission: **NEW-Q-D1**. It is not blocking
for *this* manifest (which renders nothing) but it must be resolved before any build.

## 4. Enable-chain / command-flow graph

Nodes are the six in-scope instances plus the named neighbours they depend on (neighbours are
referenced, not designed).

**Enable edges** (`X.UPSEnable` → `Y`'s start permissive; direction = permission flow, C-114):

```
MotorStarterInst8 (Overband Magnet) ─┐
ECSControlInst1 (ECS) ───────────────┴─▶ MotorVSDInst1 ─▶ AirStarInst1
FilterUnitInst2 ─┐
FilterUnitInst3 ─┴──────────────────────────────────────▶ AirStarInst1   (primary path)
MotorVSDInst3 ─┬─▶ FilterUnitInst2
               ├─▶ FilterUnitInst3
               └─▶ ECSControlInst1
MotorStarterInst6 (Ejected) ─┬─▶ MotorVSDInst3
MotorStarterInst7 (Residual) ─┤
                              └─▶ TomraControlInst1
FilterUnitInst1 (Cyclone) ─▶ (nothing — enable has no consumer, Q-A7)
```

**Acyclicity argument:** the in-scope enable edges form the DAG above. Walking it:
`MotorStarterInst6/7 → {MotorVSDInst3, TomraControlInst1} → {FilterUnitInst2, FilterUnitInst3,
ECSControlInst1} → MotorVSDInst1 → AirStarInst1`, with `MotorStarterInst8` and `ECSControlInst1`
feeding `MotorVSDInst1`. No node is reachable from itself; **no circular enable** (C-114). Note the
one edge that is *not* an enable: `TomraControlInst1 → MotorVSDInst3` is carried by the sorter's
**ready / not-faulted state**, not by its `UPSEnable` (rung A's convention-deviation delta, Q-A6) —
labelled as an **interlock/status edge**, which C-114 explicitly permits to run against the enable
direction.

**Interlock/status edges** (shutdown holds — these may run against flow, C-114 scope note):
`FilterUnitInst2/3 ← AirStarInst1.ShutdownComplete` · `FilterUnitInst1 ← ShredderControlInst1` ·
`MotorVSDInst1 ← AirStarInst1` · `MotorVSDInst3 ← ECSControlInst1` · `TomraControlInst1 ←
MotorVSDInst3`, plus the plant-level `PlantControl.FansShutdownReady` term on all three filter units.

**Direction is provisional.** No `process-topology.md` exists and rung A recorded material flow as
`not-stated-by-source` (Q-A4). The graph above is the **dependency** graph, read from the source's
own interlock table — it is not confirmed to run with material flow, so C-114's "flows with material"
claim is asserted only as provisional.

**Bidirectional equipment (C-116/C-117):** none in scope. Both VSD classes carry a reverse
capability, but neither in-scope instance has a reverse output or a reverse feedback signal
(Q-C16) — so no second enable chain is designed, and that absence is a recorded open question, not a
silent decision.

## 5. OB1 call order

**Not determinable from this run's scope, and not guessed.** The export contains no OB1 and this
design creates no mapping FCs, so the C-110 ordering (input mapping first, output mapping last, area
Mains between) cannot be stated as a fact about this project. What the design *requires* of whatever
OB1 exists:

1. input mapping runs before M1;
2. M1 (the orchestrating FC) runs after input mapping and before output mapping;
3. output mapping runs last;
4. C-111's simulation gating position — **cannot be stated**: no `DB_PLC.Simulation` exists (see §3's
   finding, NEW-Q-D1).

OB100's call list: **none exists** — NEW-Q-D1.

## 6. Pattern mapping

| Item | Carving tier | Detail |
|---|---|---|
| M1 (orchestrating FC), all six networks | **(b) pattern-composed** | `chained-permissive-enable` (ADMITTED), one instance per network. D0 shape S1. |
| M2/M3/M4 (the three FBs) | **(a) whole library block** | Reused entire and unmodified. Each carries capability beyond the specs (VSD speed control, sorter alarm words, sorter program settings) — accepted under C-606's whole-reuse exception, not a finding. |
| M5–M10 (instance DBs) | **(a)** | Existing instance DBs, unmodified. |

**Freeform %: 0%** of planned networks (counting basis: planned networks in M1 — six, all
pattern-composed). CLAUDE.md's >20%-freeform go-ahead is **not** triggered.

*Caveat that matters more than the number:* 0% freeform describes the **shape**, not the content.
Six of six networks are pattern-composed and **zero of six can currently be written**, because their
terms are contested. A low freeform figure is not evidence of readiness here.

## 7. REQ→block traceability

All 168 REQs in `gen/PlantAutoControl-bench-rerun3/requirements.md` map to **M1** (the orchestrating FC)
and/or to the FB implementing them. Restated compactly rather than row-by-row — the per-relation
detail is the D2 ledger in `code-structure.md`, which this table points into rather than duplicating.

| REQ range | Instance | Implementing item | Trace |
|---|---|---|---|
| REQ-001…025 | `FilterUnitInst2` | M1 network + M2 | D2 ledger §FilterUnitInst2 |
| REQ-026…050 | `FilterUnitInst3` | M1 network + M2 | D2 ledger §FilterUnitInst3 |
| REQ-051…074 | `FilterUnitInst1` | M1 network + M2 | D2 ledger §FilterUnitInst1 |
| REQ-075…105 | `MotorVSDInst1` | M1 network + M3 | D2 ledger §MotorVSDInst1 |
| REQ-106…139 | `MotorVSDInst3` | M1 network + M3 | D2 ledger §MotorVSDInst3 |
| REQ-140…168 | `TomraControlInst1` | M1 network + M4 | D2 ledger §TomraControlInst1 |

**Unmapped REQs: none.** Every REQ has an implementing item.
**REQs that cannot yet be implemented: 80** (the `render-BLOCKED` rows). Each is a named gap carrying
its blocking question, listed in the D2 ledger. Distribution by disposition: 60 `in-FB`, 26
`out-of-scope-obligation`, 2 `rebind`, 47 `render-BLOCKED [contested]`, 33 `render-BLOCKED
[network-stop]`.
**`out-of-scope` REQs:** REQ-017, 042, 067, 095, 126, 156 (the six alarm-indication relations) map to
"no PLC logic in this design, by design — a separate alarm artifact".

## 8. Cross-instance wiring plan (C-127)

Every cross-instance fact lives in **M1**, never inside a reusable FB. **No FB in this design
references a sibling instance by name** — verified by reading the three FBs' IR: their logic touches
only their own interface.

| Source | Destination | Relation | State |
|---|---|---|---|
| `MotorVSDInst3.IO.UPSEnable` | `FilterUnitInst2.IO.AutoStartSignal` (term) | REQ-019 | determined; render blocked |
| `MotorVSDInst3.IO.UPSEnable` | `FilterUnitInst3.IO.AutoStartSignal` (term) | REQ-044 | determined; render blocked |
| `MotorStarterInst8.IO.UPSEnable` | `MotorVSDInst1.IO.AutoStartSignal` | REQ-097 | determined; render blocked |
| `ECSControlInst1.IO.UPSEnable` | `MotorVSDInst1.IO.AutoStartSignal` | REQ-098 | determined; render blocked |
| `MotorStarterInst6/7.IO.UPSEnable` | `MotorVSDInst3` / `TomraControlInst1` `.AutoStartSignal` | REQ-130/131/159/160 | determined; render blocked |
| `TomraControlInst1.<ready?>` | `MotorVSDInst3.IO.AutoStartSignal` | REQ-128 | **UNRESOLVED — Q-C12**; candidates: `DiscreteInputs.TomraReady`, `Outputs.UPSEnable` (recommended), `HardWireSignals.Ready` (**DEAD**) |
| `TomraControlInst1.<fault?>` | `MotorVSDInst3.IO.AutoStartSignal` | REQ-129 | **UNRESOLVED — Q-C12**; candidates: `DiscreteInputs.TomraComFlt`, `Outputs.OSComFault`, `Outputs.FaultActive` (recommended, broadest) |
| `AirStarInst1.IO.ShutdownComplete` | `FilterUnitInst2/3`, `MotorVSDInst1` shutdown holds | REQ-021/046/099 | determined; **combination with the plant flag unresolved — Q-C09** |
| `ECSControlInst1.IO.ShutdownComplete` | `MotorVSDInst3` shutdown hold | REQ-132 | determined; render blocked |
| `MotorVSDInst3.IO.ShutdownComplete` | `TomraControlInst1` shutdown hold | REQ-161 | determined; render blocked |
| `ShredderControlInst1.IO.ShutdownComplete` | `FilterUnitInst1` shutdown hold | REQ-071 | determined; **Q-C09** |
| `DiscreteInputs.AirStarDCRotSen` | `MotorVSDInst1.IO.RunningFwdFB` | REQ-085 | **REBIND (BA-1)** — *not* `IO.RotationSensor`, which is DEAD-INTERFACE |
| `HMIControlSignals.SystemReset` | all six `.FaultReset` | REQ-013/038/063/090/121/153 | determined; render blocked |
| `<instance>.IO.Run` | `DiscreteOutputs.<X>Start` | REQ-018/043/068/144 | determined for 4 of 6; **the two VSDs have no start output** (REQ-096 → Q-C08) |

**Fan-ins to shared outputs:** none in this design. `DiscreteOutputs.VSDFaulrReset` would be one if
claimed — it is deliberately unclaimed (Q-C21).

## 9. Tag status

Classified with `converter tagstatus --project ir/PlantAutoControl-bench` at write time, against the
corpus at `3661b98`. **This tool classifies to MEMBER level** in this build, so the results below are
stronger than the root-level check this section historically assumed — member-level absences are
facts here, not assumptions.

| Result | Count | Names |
|---|---|---|
| **EXISTS** | 41 | all bound IO signals, all plant conditions, all 19 probed interface members |
| **MEMBER-NOT-FOUND** | 5 | `HMIControlSignals.BypassOSCRotSen` · `DiscreteInputs.FilterUnit1IsoFB` · `DiscreteInputs.TomraIsoFB` · `DiscreteOutputs.AirStarDCStart` · `DiscreteOutputs.OSCStart` |
| **PROPOSED** | 2 | `Tag_45`, `Tag_54` (the sorter's data-link words, Q-C14) |
| **MEMBER-UNCHECKED** | 0 | — |

**Nothing is designed against a `PROPOSED` tag.** The five MEMBER-NOT-FOUND results are not gaps this
design fills — each is a recorded finding (Q-C07, Q-C03 ×2, Q-C08) about a capability the specs
require and the plant does not carry. This design creates **no** new tag, DB, address or member.

## 10. Open questions

**Carried, blocking (24):** Q-A2, Q-A3 (rung A) · Q-B1 (rung B) · Q-C01, Q-C02, Q-C03, Q-C04, Q-C05,
Q-C06, Q-C07, Q-C08, Q-C09, Q-C10, Q-C11, Q-C12, Q-C13, Q-C14, Q-C16, Q-C20, Q-C21, Q-C22, Q-C23,
Q-C24, Q-C25, Q-C26 (rung C).

**Carried, non-blocking:** Q-A4, Q-A5, Q-A6, Q-A7, Q-C15, Q-C17, Q-C18, Q-C19.

**NEW (this rung):**

- **NEW-Q-D1 (blocking for any build) — the C-305/C-403/C-124 startup machinery is absent and
  unwaived.** No OB100 or equivalent startup-reset block exists in scope, and `PlantControl` (the
  `DB_PLC`-role DB) has no `Simulation : Bool`. The convention's own rule is *present in every
  design, or explicitly waived with a recorded owner-waiver citation* — there is no waiver. Absence
  of a startup reset is how S/R state survives a power cycle (C-403's site incident), and C-128 makes
  no-automatic-restart a site-wide guarantee that needs the OB100 mechanism to hold.
- **NEW-Q-D2 (non-blocking) — three interface members are declared and never read by their own FB**
  (`IO.RotationSensor` on both VSD instances, `HardWireSignals.Ready` on the sorter). C-610 requires
  a known-gap comment at the declaration of any dead signal; none exists. This is a finding about the
  **given** blocks, so it is routed rather than adjudicated here — but it directly affects two
  bindings (BA-1, BA-3) and should not be closed as cosmetic.
- **BA-1 (rebind, recorded)** — `MotorVSDInst1` motion sensor must reach `IO.RunningFwdFB`.
- **BA-3 / BA-4 (recommendations, deliberately not applied)** — for whoever answers Q-C12:
  `Outputs.UPSEnable` and `Outputs.FaultActive` are the broader guards.

---

## Gate 1 — engineer sign-off

```
Gate 1 — architecture sign-off
  Engineer:   ____________________     Date: __________

  STATUS:  *** NOT SIGNABLE ***

  Reason:  D3 (render) stopped on rung D's stop condition. This manifest carries NO render for
           any of its six equipment items, so there is nothing for gen-block-new to build.
           26 blocking questions are open (24 carried + NEW-Q-D1 + the Q-C12 pair unresolved).

  Do NOT proceed to gen-block-new / the modify pair on the strength of this file.

  To make it signable:
    1. Answer or explicitly accept the blocking set (see §10).
    2. Re-run D3 from the D2 ledger in code-structure.md — 88 relations are already unblocked
       and 33 terms already determined; they do not need re-deriving.
    3. Re-emit this manifest complete, with each item's render embedded verbatim and its
       relation-id tags on every term.
```
