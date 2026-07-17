# GenProject1 — Architecture (block manifest for gate 1)

The Design-stage artifact for GenProject1 (Kestrel Shredder Systems K150 demo panel), produced by
the `gen-architecture` skill (docs/15 pipeline skill #5). This is the **as-SHOULD-BE design derived
from the requirements register alone** — a retroactive validation run performed in greenfield
posture (the skill's own validation, docs/15 build-order step 5): the as-built corpus predates this
artifact and was deliberately **not** used as a design source. Nothing here is approved until the
gate-1 sign-off block at the end is signed.

## Provenance

- **Produced:** 2026-07-16, `gen-architecture` (retroactive validation run — see method note).
- **Inputs consumed:**
  - `gen/GenProject1/requirements.md` @ `2295c64` — the 69-REQ register: REQUIRED input, sole
    requirements source. Its C-113 classification table and open questions are consumed below.
  - `docs/06-lad-conventions.md` @ `c582829` — all rule citations read fresh this run.
  - `patterns/` @ `fb9f583` + `docs/07-pattern-library-spec.md` @ `749abba` — composition
    vocabulary (admission status read per pattern).
  - `ir/GenProject1/` @ `a11c6c4` — **tag-status verification only** (see method note).
- **Designed without `process-topology.md`** — the following decisions are provisional on it:
  material-flow direction in section 4 (inferred from register text), the interlock inventory,
  and any chain-membership statement.
- **Designed without `io-map.md`** — the following decisions are provisional on it: nothing in
  this design names a physical address (buffer members only, per C-304); address assignment and
  any panel-hardware additions (Q-11/Q-13 items) wait on the owner's address table.
- **Designed without `rfi.md`** — source contradictions surface here as carried open questions
  (Q-04, Q-06, Q-07, Q-08, Q-15) instead of resolved answers.
- **Independence declaration (informed, not blind — declared per the skill's method):** this
  design was derived from the register + doc 06 + patterns only, with all design decisions locked
  before any corpus contact; corpus contact was then grep-for-existence only (tag table, DB member
  names) — **no as-built block logic was opened before this artifact was committed**. Honesty
  notes: (1) the build brief for this validation run named expected as-built findings, and doc 06
  / the sibling skills themselves cite GenProject1 history in rule rationales — every design
  element below therefore cites the REQ or C-rule it derives from, so its basis is checkable
  independently of that exposure; (2) block-name convergence with the as-built (e.g.
  `FB_ShredderSequencer`, `FC_ControlMain`) is expected, not copied: those names appear verbatim
  in committed inputs (doc 06 C-127, `patterns/motor-dol/pattern.md`) and follow C-001..C-004
  from the register's own equipment identifiers. The full epistemics are recorded in
  `docs/notes/gen-architecture-validation-2026-07-16.md` (written after this file was committed).

## 1. Block manifest

**C-113 paradigm decisions, quoted from the register's classification table** (the register is
the classification's owner; this design applies it):

- *Shredder plant cycle* — register: "**Yes** — the same inputs mean different actions by phase:
  hopper-not-high means 'advance toward infeed start' only after forward start; reverse-running
  is a normal phase at start (REQ-005) but a recovery action mid-run (REQ-027); 'resume from the
  reverse-run step' (REQ-027) is only expressible by remembering position in the sequence" →
  **explicit stepped sequence → FB by construction (C-118)**.
- *Pusher cycle* — register: "**Yes** — extend/hold/retract phases; a mid-push pressure hold must
  resume 'from existing position' (REQ-047), and the pressure switch means different things by
  stroke (REQ-051: ignored on retract)" → **stepped sequence → FB (C-118)**.
- *IO mapping & fault annunciation* — register: "**No** — every annunciation and mapping output is
  expressible from current signals" → **combinational, FC content**.
- *In-cycle lamp* — register: "**No** — a pure OR of current running feedbacks" → **combinational,
  FC content** (deliberately NOT a sequencer interface member — see section 8).
- *Motor control (this design's addition, same test applied):* the motor blocks hold fault
  latches, hour totals, and timers across scans — Static state → FBs; they are equipment FBs per
  C-106 regardless.

| Item | Kind | Purpose (one line) |
|---|---|---|
| `OB1` (Main) | OB | Cyclic dispatch only (C-109): the five calls in section 5, no working logic. |
| `OB100` | OB | Startup: single call to `FC_StartupReset` (C-403 "executed once at PLC startup"). |
| `FC_InputMap` | FC | Maps 16 physical DIs → `DB_Input` with per-point IO-test override (C-304; input-mapping pattern). |
| `FC_OutputMap` | FC | Maps `DB_Output` → 11 physical DQs, per-point test override + per-point C-111 simulation de-energize (output-mapping pattern). |
| `FC_ControlMain` | FC | Control area-Main (C-109): ALL instance calls and ALL cross-instance wiring (C-127) — section 8 is its content spec. |
| `FC_AlarmsMain` | FC | Alarm area-Main (C-502): calls the three category FCs, nothing else. |
| `FC_EStopAlarms` | FC | E-stop/safety-circuit category (C-502: always exists): control-circuit-unhealthy annunciation from `DB_Input.ControlHealthy`. |
| `FC_GeneralAlarms` | FC | Machine fault category: motor tripped, failed-to-run ×4 (per instance), shredder blocked (REQ-056/057/058). |
| `FC_PusherAlarms` | FC | Pusher fault category (REQ-052..055), whole category gated by `Fitted` (REQ-044). |
| `FC_StartupReset` | FC | The dedicated startup-reset block (C-403/C-124/C-305): forces `DB_PLC.Simulation` FALSE, both sequencers' `Step` → 0, holds/edge-memory/in-progress counters cleared; **fault latches excluded** per C-124's carve-out. |
| `FC_Simulation` | FC | C-111 simulation driver: drives `DB_Input` members from `DB_Output` + settings when `DB_PLC.Simulation` (scope question AQ-02). |
| `FB_MotorDOL` | FB | motor-dol pattern block, used as admitted: one DOL starter (start/stop arbitration, fail-to-run/-stop, delayed shutdown, hours, telemetry, own alarm word). |
| `FB_MotorFwdRev` | FB | Forward/reverse starter for the shredder motor: direction interlock + `ReverseDelay` spin-down lockout (REQ-006, C-117), feedback supervision, overcurrent monitor (REQ-017..029), fault latching, hours. |
| `FB_ShredderSequencer` | FB | The plant-cycle stepped sequence (start ripple, hopper-high handling, overcurrent recovery, reversal-count trip, stop paths) — step sketch below. |
| `FB_PusherControl` | FB | The pusher stepped sequence (park/extend/pressure-hold/end-hold/retract), modes off/auto/manual (REQ-033), jog overlay, `Fitted` gating. |
| `UDT_MotorDOL` | UDT | motor-dol's own interface struct, as admitted with the pattern (source name `TypeDOL`/`MotorIOSet`; final in-project name is an import detail for gate 1). |
| `UDT_MotorFwdRev` | UDT | Interface for `FB_MotorFwdRev` — section 2. |
| `UDT_ShredderSeq` | UDT | Interface for `FB_ShredderSequencer` — section 2 (carries `Step` per C-118). |
| `UDT_Pusher` | UDT | Interface for `FB_PusherControl` — section 2 (carries `Step` per C-118). |
| `DB_Input` | DB | Discrete-input buffer (C-304; db-inputs pattern layout: `Test[]`, `SpareDI[]`, named points). Exists. |
| `DB_Output` | DB | Discrete-output buffer (C-304; db-outputs layout). Exists. |
| `DB_AnalogInput` | DB | Analog-input buffer (C-304 analog side): `ShredderMotorCurrent`. Exists; **no physical AI channel** (Q-11). |
| `DB_PLC` | DB | C-305: `Simulation : Bool` start FALSE + system data. **Mandatory — see section 3.** |
| `DB_Controls` | DB | C-306 operator-command surface: the five existing HMI-written members, nothing else. Exists. |
| `DB_Settings` | DB | C-307 post-split scope: plant-wide-only — target contents are the (proposed, Q-04-blocked) machine-type selection + per-type overcurrent table. Exists (current members migrate — AQ-01). |
| `DB_Alarms` | DB | C-501 category words: `EStopAlarm0`, `GeneralAlarm0`, `PusherAlarm0`. DB exists; these word names are this design's (proposed). |
| `DB_Timers` | DB | C-407 home for standalone timers outside equipment FBs (the C-504 suppression TONs in alarm FCs). |
| `iDB_MotorDOL_DIS` / `_IFC` / `_PSH` | iDB | Instances: discharge conveyor, infeed conveyor, pusher power pack (C-003/C-004 identifiers from the register's tag vocabulary). |
| `iDB_MotorFwdRev_SHR` | iDB | The shredder motor instance. |
| `iDB_ShredderSequencer_SYS` | iDB | The plant-cycle sequencer instance (singleton, equipment = the system). |
| `iDB_PusherControl_PSH` | iDB | The pusher instance. |

33 items: 2 OB, 9 FC, 4 FB, 4 UDT, 8 DB, 6 iDB.

**Area-Main structure (C-109, incl. its documented exception):** OB1 calls exactly:
`FC_InputMap` and `FC_OutputMap` **directly** (C-109's 2026-07-16 IO-mapping exception — no
`FC_MapIOMain` wrapper; their position is pinned by C-110), `FC_Simulation` (mapping-layer
machinery, same exception family, position pinned next to the input map by C-111),
`FC_ControlMain` and `FC_AlarmsMain` (area Mains — each calls only its own area's blocks). No
`FC_ComsMain` (no comms requirement in the register — C-606: no unrequested capability).

**Draft step legends** (design sketch — the coding stage owns the final C-120 header legend;
steps ascend by 10, idle = 0 per C-119; transitions are C-121 gated MOVEs):

`FB_ShredderSequencer`: 0 idle · 10 pre-start siren 10 s (REQ-003) · 20 start discharge +
confirm (REQ-004) · 30 shredder reverse 6 s (REQ-005; re-entered by REQ-027) · 40 spin-down wait
— advances on motor `Ready`, the 8 s lives in the motor's `ReverseDelay`, measured once (REQ-006,
C-409) · 50 shredder forward + confirm (REQ-007) · 60 hopper gate: wait not-high, then 5 s
(REQ-008/009/015) · 70 start infeed + confirm (REQ-010) · 80 running — supervises hopper-high
(→ 60 with infeed dropped + pusher cycle request; REQ-014/034) and overcurrent (→ 90;
REQ-023..025) · 90 overcurrent pause 4 s, reversal count++ (→ 30; REQ-026/027; count ≥ threshold
in window → latched trip fault → 0; REQ-028). Stop path from any step (REQ-012/013/062/063):
enables dropped, → 0. Start (0 → 10) on start-PB edge (C-404 constructed) AND downstream
available AND control healthy AND no trip fault (REQ-001/002/062).

`FB_PusherControl`: 0 idle (idle means "no phase active" — `AtPark` is the limit switch, never
implied by the step number) · 10 return-to-park (from mode-off REQ-037, pre-start-complete
REQ-045, blocked-fault return REQ-048, sequencer park request REQ-025; pressure ignored REQ-051;
`ParkedTimeout` C-122 fault REQ-055) · 20 extend (pressure > 0.5 s → 30 REQ-046;
`EndTravelTimeout` C-122 fault REQ-054) · 30 pressure hold — a C-123 hold, non-latching: outputs
off, step frozen; clear + 5 s → 20 (REQ-047); 5 trips same cycle → latched blocked fault + → 10
(REQ-048), lockout until `FaultReset` (REQ-050), never stops the shredder (REQ-049) · 40
end-travel hold 2 s (REQ-032) · 50 retract to park (pressure ignored REQ-051) → 0. Jog (hand) is
**combinational by C-113's own test** ("hold the button to transition... on release it stops
where it is" — REQ-039: expressible from current signals; the 1 s warn is a TON, not phase
memory): gated solenoid drive from step 0 only, warn-then-move (REQ-038..041), independent of
the plant cycle (REQ-040), power pack running required first (REQ-030). Plant stop (REQ-012):
outputs off, → 0.

## 2. Interface definitions

**C-115 handshake vocabulary** — taken from the admitted site pattern's real members (motor-dol),
not doc 06's illustrative names; every equipment FB wires identically unless a deviation is named
here:

| Vocabulary member | Dir | `FB_MotorDOL` (pattern) | `FB_MotorFwdRev` | `FB_PusherControl` |
|---|---|---|---|---|
| `AutoStartSignal` | in | native | **split: `AutoStartFwd` / `AutoStartRev`** (C-116: one enable per direction) | **replaced by cycle triggers** (`CycleRequestAuto` etc.) — a stroke cycle, not a run level; deviation named here |
| `Shutdown` | in | native | native | `StopCmd` (plant stop → outputs off + step 0) |
| `RunningFB` | in | native | **split: `RunningFwdFB` / `RunningRevFB`** | n/a (limit switches instead: `HomeLimit`/`EndLimit`) |
| `SystemHealthy` | in | native | native | native |
| `InhibitMotor` | in | native | native | n/a (`Fitted` FALSE is the pusher's inhibit) |
| `PreStartDone` | in | native | native | native (drives REQ-045 park-on-completion) |
| `FaultReset` | in | native | native | native |
| `Run` | out | native | **split: `RunFwd` / `RunRev`** | **split: `ExtendCmd` / `RetractCmd`** (two solenoids) |
| `UPSEnable` | out | native (IFC's drives REQ-011) | not used (no downstream enable consumer) | n/a |
| `ShutdownComplete` | out | native | native | n/a |
| `FTR` / `FTS` / `FaultActive` | out | native | native (+`OvercurrentEvent`) | fault bits below + aggregate `FaultActive` |
| `HrsRun` | out | native (REQ-065) | native | n/a |
| `Telemetry` / `Alarm` | out | native | native | native |

**`UDT_MotorFwdRev`** (all members carry one-line comments at coding time — C-605 is error
severity): ins `AutoStartFwd`, `AutoStartRev`, `Shutdown`, `RunningFwdFB`, `RunningRevFB`,
`ExternalFault` (DI7 soft-start relay), `SystemHealthy`, `PreStartDone`, `FaultReset`,
`MotorCurrent : Real` (from `DB_AnalogInput`); outs `RunFwd`, `RunRev`, `Running`, `Ready`
(both feedbacks low AND `ReverseDelay` elapsed AND no fault — the C-117 stopped-before-reversal
permissive, and REQ-006's wait condition), `OvercurrentEvent` (one-shot per event, C-404
constructed edge), `FTR`, `FaultActive`, `HrsRun`, `Telemetry`, `Alarm`; settings **[S]**
`ReverseDelay` (8.0 — Q-09), `FTTime` (run-confirm window, unconfigured — Q-02),
`OvercurrentSetpointMedium` **[S]**, `OvercurrentSetpointHigh` **[S]**, `OvercurrentMediumDelay`
**[S]**, `OvercurrentHighDelay` **[S]** (all four unconfigured — Q-02),
`OvercurrentSpinUpAllowance` **[S]** (3.0, REQ-029). Overcurrent levels are two named TONs
(C-406/C-408), medium and high, armed only while running forward past the spin-up window.

**`UDT_ShredderSeq`**: ins `StartCmd`, `StopCmd`, `ControlHealthy` (REQ-062), `DownstreamRunning`
(REQ-002/013), `HopperHigh`, `FaultReset`, `DischargeRunning`, `InfeedRunning`,
`ShredderReady`, `ShredderRunningFwd`, `ShredderRunningRev`, `OvercurrentEvent`,
`MotorFaultAny` (REQ-063); outs `Step : Int` (C-118 — in this UDT, nowhere else),
`DischargeEnable`, `InfeedEnable`, `ShredderFwdEnable`, `ShredderRevEnable`, `SounderCmd`,
`PreStartComplete`, `PusherCycleRequest` (REQ-014/034), `PusherParkRequest` (REQ-025),
`UpstreamPermit` (cycle-side condition for REQ-011; the 3 s lives in the IFC motor instance),
`ShredderBlockedFault` (latched, REQ-028/058), `ReversalCount : Int` (C-401 explicit counting;
REQ-060 HMI binding), `Telemetry`; settings **[S]** `PreStartSounderTime` (10.0),
`ShredderReverseRunTime` (6.0), `InfeedRestartDelay` (5.0), `ReversalRetryPauseTime` (4.0),
`ReversalWindowTime` (180.0), `ReversalCountThreshold` (5). Per C-122, each timed step's `PT`
comes from these members of this block's own UDT.

**`UDT_Pusher`**: ins `Mode : Int` (REQ-033; semantics of hand vs the three modes = Q-15),
`CycleRequestAuto`, `ManualCycleCmd`, `RemoteCycleCmd` (**field source proposed** — Q-03; member
reserved, wiring blocked until the tag exists), `LocalRemote` (DI16 — semantics Q-03),
`JogExtendCmd`, `JogRetractCmd`, `StopCmd`, `PreStartDone`, `SystemHealthy`, `FaultReset`,
`HomeLimit`, `EndLimit`, `HighPressure`, `PowerPackRunning` (REQ-030); outs `Step : Int`
(C-118), `ExtendCmd`, `RetractCmd`, `PowerPackRunRequest`, `WarnSounderCmd` (REQ-041),
`AtPark`, `FaultBothLimits` (REQ-052), `FaultBlocked` (REQ-053), `FaultEndTravelTimeout`
(REQ-054), `FaultParkedTimeout` (REQ-055), `FaultActive`, `PressureTripCount : Int`,
`Telemetry`; settings **[S]** `Fitted` (TRUE — REQ-042; the C-604 named-source constant for
not-fitted machines), `EndTravelHoldTime` (2.0), `EndTravelTimeout` (unconf. — Q-02),
`ParkedTimeout` (unconf. — Q-02), `PressureTripConfirmTime` (0.5), `PressureClearResumeDelay`
(**unconfigured with a spec-stated 5 s** — REQ-047/Q-02: 5.0 is the REQ-derived default for the
engineer to set), `PressureTripCountThreshold` (5), `JogWarningTime` (1.0). C-122/C-125: the two
timeout timers are multi-instance here, their fault bits live in this UDT, not private Statics.

**`UDT_MotorDOL` (pattern)**: as admitted — the three per-instance settings **[S]** carry these
project values: `iDB_MotorDOL_DIS.FTTime` ← the discharge confirm window (REQ-004,
unconfigured — Q-02); `iDB_MotorDOL_IFC.EnableUPSTime` ← 3.0 (REQ-011/016 — the upstream-enable
delay is the pattern's own member, measured once, C-409); `iDB_MotorDOL_PSH.ShutdownTime` ← the
power-pack run-on (REQ-031, unconfigured — Q-02).

**C-307/C-308 one-writer statement (applies to every [S] above):** each setting lives in exactly
one home — its owning instance's UDT (C-307's per-instance scope: the faceplate tunes its own
block; commissioning defaults become iDB start values per C-309). The HMI is the only writer;
logic reads only; **no orchestrator scan-copy exists anywhere in this design** — with single
homes there is no second copy to synchronize, so the C-308 trap (a cyclic MOVE from
`DB_Settings` silently reverting faceplate edits) is impossible by construction, not by
discipline. `DB_Settings` retains only what no single faceplate owns (section 3).

## 3. DB landscape (C-30x)

| DB | Rule basis | Contents | Retentivity |
|---|---|---|---|
| `DB_Input` | C-304, db-inputs pattern | `Test[0..22]`, `SpareDI[1..7]`, 16 named points (PascalCase per C-001's 2026-07-16 revision — the corpus's Snake_Case members predate it; AQ-01 covers the rename) | none |
| `DB_Output` | C-304, db-outputs pattern | `Test[0..18]`, `SpareDQ[1..7]`, 11 named points | none |
| `DB_AnalogInput` | C-304 (analog own DB) | `ShredderMotorCurrent : Real` — buffer exists, **no AI channel** (Q-11); its map FC is deliberately NOT designed (hard rule 3: no invented hardware) | none |
| `DB_PLC` | C-305 | `Simulation : Bool` start FALSE (force-reset in `FC_StartupReset`), misc. system data | none |
| `DB_Controls` | C-306 | `PusherMode`, `PusherManualCycleCmd`, `PusherJogExtendCmd`, `PusherJogRetractCmd`, `FaultReset` — HMI-written command surface; no logic-internal state | none |
| `DB_Settings` | C-307 (post-split) | Target: `MachineType` selection + per-type overcurrent table (both **proposed**, blocked Q-04). Existing members migrate to their owning UDTs (AQ-01). Kept even if momentarily empty — it is the standing plant-wide home | retentive |
| `DB_Alarms` | C-501 | `EStopAlarm0 : Word`, `GeneralAlarm0 : Word`, `PusherAlarm0 : Word` — one bit per alarm network, slice-written under C-501's two conditions | none |
| `DB_Timers` | C-407 | Named standalone TONs used by the alarm FCs (C-504 suppression delays); equipment timers stay multi-instance in their iDBs | none |
| iDBs (6) | C-003 | Per section 1; settings start values = commissioning defaults (C-309) | fault latches only (C-124 carve-out) |

**Startup machinery — PRESENT, not waived.** No owner waiver is recorded anywhere in this
project's inputs, so C-305/C-403/C-124 apply in full: `OB100` → `FC_StartupReset`, which
force-writes (regardless of retentivity): `DB_PLC.Simulation` := FALSE (C-305);
`iDB_ShredderSequencer_SYS.IO.Step` := 0 and `iDB_PusherControl_PSH.IO.Step` := 0 plus their
holds, in-progress counters (`ReversalCount`, `PressureTripCount`) and edge-memory (C-124);
every S/R-written bit the coding stage introduces (C-403 — the coding stage extends this block
whenever it writes an SCoil/RCoil). **Excluded by C-124's carve-out:** latched fault bits
requiring human acknowledgement (`FaultActive`/`FTR`-class, `ShredderBlockedFault`,
`FaultBlocked`) — a fault needing a human still needs one after a power cycle. Q-01 (required
plant behavior after a mid-cycle power cycle) is carried: this design's restart state is
"everything idle, faults preserved, fresh start required," which REQ-062's fresh-start principle
supports, but the owner statement is still owed.

**Simulation machinery (C-111/C-112):** `DB_PLC.Simulation` gates the `FC_InputMap` call off and
`FC_Simulation` on; `FC_OutputMap` keeps running with a per-point `-|/|-` on
`DB_PLC.Simulation` so all physical outputs de-energize while simulating. `C-112`'s HMI banner
is an HMI-stage deliverable (bind `DB_PLC.Simulation`). Demo-scope question: AQ-02.

## 4. Command-flow graph (C-114/C-116)

**Material flow (provisional — no `process-topology.md`; inferred from register text):**
upstream supplier → infeed conveyor → hopper (pusher assists transfer) → shredder → discharge
conveyor → downstream plant.

**This is a stepped plant (register C-113), so this section is a command-flow graph, not a
permissive chain** — the start ripple lives in `FB_ShredderSequencer`'s steps (which run
downstream-first: discharge → shredder → infeed → upstream, per the FuncDesc-derived REQ order),
not in a C-114 equipment-to-equipment enable chain. C-114's checkable content here:

- **Enable edges (all one direction: sequencer → equipment; a tree, acyclic by construction):**
  `SEQ.DischargeEnable → MotorDOL_DIS.AutoStartSignal`; `SEQ.ShredderFwdEnable →
  MotorFwdRev_SHR.AutoStartFwd`; `SEQ.ShredderRevEnable → MotorFwdRev_SHR.AutoStartRev`;
  `SEQ.InfeedEnable → MotorDOL_IFC.AutoStartSignal`; `SEQ.PusherCycleRequest/ParkRequest →
  PusherControl_PSH`; `PusherControl_PSH.PowerPackRunRequest → MotorDOL_PSH.AutoStartSignal`;
  `MotorDOL_IFC.UPSEnable → DB_Output.EnableUpstream` (the chain's tail leaves the PLC —
  REQ-011). No equipment enables the sequencer; no equipment enables a sibling except the two
  listed request edges, both one-directional. **No cycle exists.**
- **Status/interlock edges (C-114's exempt class — may run against command direction):** running
  feedbacks, `Ready`, `FaultActive`, `OvercurrentEvent` → sequencer; `PowerPackRunning` →
  pusher (proof-of-running permissive, REQ-030 — not an enable, so pusher↔power-pack is not a
  circular pair); `DownstreamRunning` → sequencer head (REQ-002/013 — a downstream interlock
  feeding upstream, exactly the exempt shape).
- **C-116 (bidirectional shredder):** one enable per direction (`ShredderFwdEnable` /
  `ShredderRevEnable`), asserted by disjoint step sets (fwd: 50..90 family; rev: 30) — mutually
  exclusive by step construction, and `FB_MotorFwdRev` independently refuses both-at-once and
  enforces defined behavior (stopped) when neither is asserted. **C-117:** direction change only
  through `Ready` (both feedbacks low + `ReverseDelay` elapsed) — the affected equipment is
  stopped, at minimum, before any reversal; no on-the-fly reversal path exists in the step
  graph.

## 5. OB1 call order (C-110)

| # | Call | Gating |
|---|---|---|
| 1 | `FC_InputMap` | `-|/|-` on `DB_PLC.Simulation` (C-111) — input mapping FIRST (C-110) |
| 2 | `FC_Simulation` | `-| |-` on `DB_PLC.Simulation` |
| 3 | `FC_ControlMain` | always |
| 4 | `FC_AlarmsMain` | always |
| 5 | `FC_OutputMap` | always — output mapping LAST (C-110); per-point simulation de-energize inside |

`OB100`: single call to `FC_StartupReset`. An analog input map is deliberately absent (Q-11 — no
AI hardware to map; hard rule 3).

## 6. Pattern mapping and freeform share

| Manifest item | Pattern (kind, admission) or freeform | Structured by |
|---|---|---|
| `FC_InputMap` | **input-mapping** (kind 2, proposed — not yet admitted) | pattern doc |
| `FC_OutputMap` | **output-mapping** (kind 2, proposed) | pattern doc |
| `DB_Input` / `DB_Output` | **db-inputs / db-outputs** (kind 3, proposed) | pattern doc |
| `FB_MotorDOL` + `UDT_MotorDOL` + 3 iDBs | **motor-dol** (kind 1, **ADMITTED** 2026-07-15) | pattern block used as-is |
| `FB_MotorFwdRev` (+UDT, iDB) | freeform — **AQ-03: site block `MotorFwdRevSystem` exists per motor-dol's own pattern.md; C-108 prefers importing it over fresh invention** | C-106/C-116/C-406/C-408, motor-dol's shape by analogy |
| `FB_ShredderSequencer` (+UDT, iDB) | freeform | C-118..C-125 (the stepped-sequence rule set) |
| `FB_PusherControl` (+UDT, iDB) | freeform | C-118..C-125 |
| `FC_ControlMain` | freeform (wiring-network shape analogous to chained-permissive-enable's per-equipment skeleton — analogy only; this is not a permissive-chain plant) | C-109/C-127/C-601 |
| `FC_AlarmsMain` + 3 category FCs | freeform | C-501/C-502/C-504 shapes |
| `FC_StartupReset`, `FC_Simulation`, `OB1`, `OB100`, `DB_PLC`, `DB_Timers`, `DB_Alarms`, `DB_Settings` (target state) | freeform, convention-shaped | C-403/C-124/C-305/C-111/C-110/C-407/C-501/C-307 |

**Freeform share: ~78% by planned networks** (pattern-covered ≈ 22 networks: input-mapping ~6,
output-mapping ~2, motor-dol block 14; freeform ≈ 78 across the two sequencers, `FB_MotorFwdRev`,
`FC_ControlMain`, the alarm FCs, startup/simulation, OB shells). By manifest items: 24 of 33
(~73%). Counting basis: planned-network estimate at design time.

**>20% flag (CLAUDE.md workflow step 3): RAISED, loudly.** This build is majority-freeform —
exactly docs/15's cause 3 (thin pattern coverage). Gate-1 sign-off of this manifest is where the
engineer gives or refuses the freeform go-ahead; no freeform rung is written before that.
Mitigations on the table: AQ-03 (import the site fwd/rev block instead of freeform), and the S8
harvest — a signed-off sequencer and pusher are the obvious next pattern candidates, shrinking
this number for the next build.

## 7. REQ → block traceability

Every register REQ maps to the manifest item(s) that will implement it; **BLOCKED** = named gap
with its blocking question — the REQ is mapped, its implementation is not startable.

| REQ | Manifest item(s) — mechanism | Status / gap |
|---|---|---|
| REQ-001 | `FB_ShredderSequencer` S0→S10 (start-PB edge, C-404) ← `FC_ControlMain` ← `DB_Input.CycleStart` | mapped (Q-06 press-vs-hold, Q-14 control-on carried) |
| REQ-002 | Sequencer S0→S10 permissive `DownstreamRunning` | mapped |
| REQ-003 | Sequencer S10 TON (`PreStartSounderTime`=10) → `SounderCmd` → sounder fan-in (§8) | mapped |
| REQ-004 | Sequencer S20 + `iDB_MotorDOL_DIS` (confirm via `DischargeRunning`; window = DIS `FTTime`) | mapped; window unconfigured (Q-02) |
| REQ-005 | Sequencer S30 (`ShredderReverseRunTime`=6) → `ShredderRevEnable` → `FB_MotorFwdRev` | mapped |
| REQ-006 | `FB_MotorFwdRev.ReverseDelay`=8 lockout; sequencer S40 waits `Ready` — one interval, one place (C-409) | mapped (Q-09 confirms the home) |
| REQ-007 | Sequencer S50 → `ShredderFwdEnable` | mapped |
| REQ-008 | Sequencer S60 gate on NOT `HopperHigh` | mapped |
| REQ-009 | Sequencer S60 TON (`InfeedRestartDelay`=5) | mapped |
| REQ-010 | Sequencer S70 → `InfeedEnable` → `iDB_MotorDOL_IFC` | mapped |
| REQ-011 | `iDB_MotorDOL_IFC.UPSEnable` (`EnableUPSTime`=3) → `DB_Output.EnableUpstream` | mapped (Q-07 carried) |
| REQ-012 | `FC_ControlMain` named `StopCmd` (NC polarity handled at derivation) → sequencer stop→S0, all motor `Shutdown`, pusher `StopCmd`→S0 | mapped |
| REQ-013 | Same stop path, `NOT DownstreamRunning` OR-term | mapped (Q-08 recovery behavior) |
| REQ-014 | Sequencer S80→S60: `InfeedEnable` dropped, shredder enables held, `PusherCycleRequest` raised | mapped |
| REQ-015 | Sequencer S60 (same gate + same 5 s as REQ-009 — one setting) → S70 | mapped |
| REQ-016 | Automatic via `UPSEnable` following infeed running + 3 s (same member as REQ-011) | mapped |
| REQ-017 | `FB_MotorFwdRev` overcurrent monitor on `DB_AnalogInput.ShredderMotorCurrent` (level+time, distinct from overload) | mapped; **arming BLOCKED Q-11** (no AI hardware) |
| REQ-018 | `DB_Settings.MachineType` (**proposed**) | **BLOCKED Q-04** |
| REQ-019 | `DB_Settings` per-type setpoint table (**proposed**); existing single pair = degenerate one-type form | **BLOCKED Q-04** (+Q-02 values) |
| REQ-020 | Per-instance monitor design ready; second motor's instrumentation **proposed** | **BLOCKED Q-05** |
| REQ-021 | `FB_MotorFwdRev` medium TON (`OvercurrentSetpointMedium`/`MediumDelay`) | mapped; values Q-02 |
| REQ-022 | `FB_MotorFwdRev` high TON (`OvercurrentSetpointHigh`/`HighDelay`) | mapped; values Q-02 |
| REQ-023 | Sequencer S80→S90 on `OvercurrentEvent`: `ShredderFwdEnable` dropped | mapped |
| REQ-024 | Same transition: `InfeedEnable` dropped | mapped |
| REQ-025 | Same transition: `PusherParkRequest` → pusher S10 | mapped |
| REQ-026 | Sequencer S90 TON (`ReversalRetryPauseTime`=4) | mapped |
| REQ-027 | Sequencer S90→S30 (resume at the reverse-run step) | mapped |
| REQ-028 | Sequencer reversal counter (C-401 Int + constructed edge) vs `ReversalCountThreshold`=5 within `ReversalWindowTime`=180 → latched trip → S0 | mapped (annunciation REQ-058) |
| REQ-029 | `FB_MotorFwdRev.OvercurrentSpinUpAllowance`=3 arms the monitor late | mapped |
| REQ-030 | Pusher permissive `PowerPackRunning`; `PowerPackRunRequest` raised before motion | mapped |
| REQ-031 | `iDB_MotorDOL_PSH.ShutdownTime` (pattern's delayed-shutdown member) | mapped; value Q-02 |
| REQ-032 | Pusher S20→S40 (2 s `EndTravelHoldTime`)→S50→S0 | mapped |
| REQ-033 | `DB_Controls.PusherMode` → `UDT_Pusher.Mode` | mapped (Q-15 hand semantics) |
| REQ-034 | Sequencer `PusherCycleRequest` → pusher (Mode=auto) | mapped |
| REQ-035 | `DB_Controls.PusherManualCycleCmd` → pusher (Mode=manual) | mapped |
| REQ-036 | `UDT_Pusher.RemoteCycleCmd` reserved; field DI **proposed** | **BLOCKED Q-03** (no coding against a proposed tag) |
| REQ-037 | Pusher Mode=off → S10 (retract to park) | mapped |
| REQ-038 | `DB_Controls.PusherJogExtendCmd`/`JogRetractCmd` → jog gating | mapped (Q-15) |
| REQ-039 | Jog is hold-to-move combinational gating (C-113 quote in §1) | mapped |
| REQ-040 | Jog path independent of sequencer state (needs only pack running + healthy + fitted) | mapped |
| REQ-041 | Pusher `JogWarningTime`=1 TON → `WarnSounderCmd`, held while moving → sounder fan-in | mapped |
| REQ-042 | `UDT_Pusher.Fitted` [S] (C-604 named source) | mapped (migration AQ-01) |
| REQ-043 | `Fitted` FALSE gates all pusher outputs, pack request, and feedback supervision | mapped |
| REQ-044 | `FB_PusherControl` suppresses fault bits + `FC_PusherAlarms` category gated by `Fitted` | mapped |
| REQ-045 | Pusher: `PreStartDone` rising edge AND NOT `AtPark` → S10 | mapped |
| REQ-046 | Pusher S20→S30 on `HighPressure` > `PressureTripConfirmTime`=0.5 | mapped |
| REQ-047 | Pusher S30→S20 on pressure clear + `PressureClearResumeDelay` (spec says 5 s; member unconfigured) | mapped; **value gap Q-02** (5.0 recorded as the REQ-derived default for the engineer) |
| REQ-048 | Pusher trip counter (C-401) ≥ `PressureTripCountThreshold`=5 same cycle → `FaultBlocked` latched + S10 | mapped |
| REQ-049 | `FaultBlocked` is pusher-local: no wire from it into the sequencer stop path — absence is by design and reviewable in §8 | mapped |
| REQ-050 | `FaultBlocked` latched (C-123), cleared only by `FaultReset`; gates all cycle triggers | mapped |
| REQ-051 | Pressure supervision armed in S20 only (step-scoped by construction; S10/S50 ignore it) | mapped |
| REQ-052 | Pusher `FaultBothLimits` (HomeLimit AND EndLimit) → `FC_PusherAlarms` → `PusherAlarm0` bit | mapped |
| REQ-053 | `FaultBlocked` → `FC_PusherAlarms` → `PusherAlarm0` bit | mapped |
| REQ-054 | Pusher C-122 timer `EndTravelTimeout` → `FaultEndTravelTimeout` → alarm bit | mapped; value Q-02 |
| REQ-055 | Pusher C-122 timer `ParkedTimeout` → `FaultParkedTimeout` → alarm bit | mapped; value Q-02 |
| REQ-056 | `DB_Input.MotorFault` → `FB_MotorFwdRev.ExternalFault` → `FC_GeneralAlarms` → `GeneralAlarm0` bit | mapped |
| REQ-057 | Four per-instance `FTR` bits → four named `GeneralAlarm0` bits (identification per motor) | mapped; second shredder motor **BLOCKED Q-05** |
| REQ-058 | Sequencer `ShredderBlockedFault` → `FC_GeneralAlarms` bit | mapped |
| REQ-059 | Overload as a distinct annunciation | **BLOCKED Q-10** (single fault input cannot distinguish; no hardware to invent) |
| REQ-060 | `UDT_ShredderSeq.ReversalCount` for HMI binding; outside-panel display + flashing lamp | display binding mapped; panel hardware **BLOCKED Q-13** |
| REQ-061 | Out-of-scope (hardwired safety): **no PLC item, by design** — `ControlHealthy` is consumed as a permissive/annunciation source only; internals never elaborated (hard rule 2) | recorded |
| REQ-062 | Sequencer: `ControlHealthy` loss → stop path → S0; re-entry only via fresh `StartCmd` edge | mapped |
| REQ-063 | `FC_ControlMain` `MotorFaultAny` (OR of 4 `FaultActive`) → sequencer stop path | mapped; windows = per-instance `FTTime` (Q-02) |
| REQ-064 | HMI binding of `DB_Settings.MachineType` | **BLOCKED Q-04** |
| REQ-065 | Motor instances' `HrsRun` (pattern member) — HMI binding | mapped |
| REQ-066 | Zeroable job-hours value + reset command | **BLOCKED Q-12** (home candidates: `DB_PLC` or sequencer UDT — owner decides) |
| REQ-067 | Same selection mechanism as REQ-018 | **BLOCKED Q-04** (2-vs-3 types unresolved) |
| REQ-068 | HMI binding: `FB_MotorFwdRev.RunFwd`-side feedbacks + `ShredderBlockedFault` | mapped (HMI-stage deliverable) |
| REQ-069 | `FC_ControlMain` combinational network: `DB_Output.InCycle` := OR of the five field running feedbacks in `DB_Input` — per the ruling, field feedbacks, not PLC commands; deliberately not a sequencer member | mapped |

Coverage: **69/69 REQs mapped** — 59 implementable now, 9 BLOCKED on carried questions
(REQ-018/019/020/036/059/064/066/067 + REQ-017's arming), 1 out-of-scope (REQ-061). No REQ
dropped.

## 8. Cross-instance wiring plan (C-127 — all of it in `FC_ControlMain`)

Every cross-instance fact in the project lives in `FC_ControlMain`; no FB names a sibling
instance inside its own logic (C-127 — a reusable FB that would need one is a design error, and
none below does). Derived named bits first (C-601: one write, many reads):

- `StopCmd` := NOT `DB_Input.CycleStop` (NC field device — active-low) OR NOT
  `DB_Input.DownstreamRunning` (REQ-012/013)
- `MotorFaultAny` := DIS.`FaultActive` OR IFC.`FaultActive` OR PSH.`FaultActive` OR
  SHR.`FaultActive` (REQ-063)

| From | To | Why (REQ / rule) |
|---|---|---|
| `DB_Input.CycleStart` | SEQ.`StartCmd` | REQ-001 |
| `StopCmd` | SEQ.`StopCmd`; pusher.`StopCmd`; each motor's `Shutdown` (OR its own enable loss) | REQ-012/013 |
| `DB_Input.ControlHealthy` | SEQ.`ControlHealthy`; `SystemHealthy` of all four motors + pusher | REQ-062; C-115 |
| `DB_Input.DownstreamRunning` | SEQ.`DownstreamRunning` | REQ-002/013 |
| `DB_Input.HopperLevelHigh` | SEQ.`HopperHigh` | REQ-008/014 |
| `DB_Input.DischargeConvRunning` | SEQ.`DischargeRunning`; DIS.`RunningFB` | REQ-004 |
| `DB_Input.InfeedConvRunning` | SEQ.`InfeedRunning`; IFC.`RunningFB` | REQ-010 |
| `DB_Input.ShredderRunFwdFB` / `RunRevFB` | SHR.`RunningFwdFB`/`RunningRevFB`; SEQ.`ShredderRunningFwd`/`Rev` | REQ-005/007 |
| `DB_Input.PusherPowerPackRunning` | pusher.`PowerPackRunning`; PSH.`RunningFB` | REQ-030 |
| `DB_Input.MotorFault` | SHR.`ExternalFault` | REQ-056 |
| `DB_Input.PusherHomeLimit` / `FullTravelLimit` / `HighPressure` | pusher.`HomeLimit`/`EndLimit`/`HighPressure` | REQ-032/046/052 |
| `DB_Input.PusherLocalRemote` | pusher.`LocalRemote` | Q-03 (semantics open) |
| `DB_AnalogInput.ShredderMotorCurrent` | SHR.`MotorCurrent` | REQ-017 (arming Q-11) |
| `DB_Controls.PusherMode`/`PusherManualCycleCmd`/`PusherJogExtendCmd`/`PusherJogRetractCmd` | pusher ins | REQ-033/035/038 |
| `DB_Controls.FaultReset` | `FaultReset` of SEQ, pusher, all motors | REQ-050 + general reset |
| SEQ.`DischargeEnable` | DIS.`AutoStartSignal` | REQ-004 |
| SEQ.`InfeedEnable` | IFC.`AutoStartSignal` | REQ-010/014 |
| SEQ.`ShredderFwdEnable` / `ShredderRevEnable` | SHR.`AutoStartFwd`/`AutoStartRev` | REQ-005/007, C-116 |
| SEQ.`PreStartComplete` | pusher.`PreStartDone`; motors' `PreStartDone` | REQ-045; C-115 |
| SEQ.`PusherCycleRequest` | pusher.`CycleRequestAuto` | REQ-014/034 |
| SEQ.`PusherParkRequest` | pusher park trigger | REQ-025 |
| SHR.`Ready` | SEQ.`ShredderReady` | REQ-006 (S40 advance) |
| SHR.`OvercurrentEvent` | SEQ.`OvercurrentEvent` | REQ-023 |
| `MotorFaultAny` | SEQ.`MotorFaultAny` | REQ-063 |
| pusher.`PowerPackRunRequest` | PSH.`AutoStartSignal` | REQ-030/031 |
| DIS.`Run` / IFC.`Run` / PSH.`Run` | `DB_Output.RunDischargeConv`/`RunInfeedConv`/`RunPowerPack` | run commands out |
| SHR.`RunFwd` / `RunRev` | `DB_Output.RunShredderFwd`/`RunShredderRev` | REQ-005/007 |
| pusher.`ExtendCmd` / `RetractCmd` | `DB_Output.PusherExtend`/`PusherRetract` | REQ-032 |
| IFC.`UPSEnable` | `DB_Output.EnableUpstream` | REQ-011/016 |
| SEQ.`SounderCmd` OR pusher.`WarnSounderCmd` | `DB_Output.PreStartSounder` — **one fan-in network, single writer of the buffer member (C-403 plain coil)** | REQ-003/041 |
| OR of the five field running feedbacks (`ShredderRunFwdFB`, `ShredderRunRevFB`, `DischargeConvRunning`, `InfeedConvRunning`, `PusherPowerPackRunning`) | `DB_Output.InCycle` | REQ-069 (field-true, per the owner ruling) |
| `DB_Controls.FaultReset` | `DB_Output.MotorFaultReset` (momentary pass-through to the soft start) | recovery path for REQ-056; panel provides the output (AQ-05, rel. Q-14) |

Alarm-side cross-instance reads (in the alarm FCs, read-only, one bit per network per C-501):
pusher fault bits + `Fitted` → `PusherAlarm0` (REQ-044/052..055); motor `FTR`×4 +
SHR.`ExternalFault`-fault + SEQ.`ShredderBlockedFault` → `GeneralAlarm0` (REQ-056..058); NOT
`DB_Input.ControlHealthy` → `EStopAlarm0`. Cause→consequence suppression (C-504 — e.g. healthy
loss suppressing motor-fault echoes) is specified at the `gen-alarm-design` stage; `DB_Timers`
hosts its TONs.

## 9. Tag status

Grep-verified 2026-07-16 against `ir/GenProject1/` @ `a11c6c4` (verification files: tag table +
DB member names only — no block logic opened; see Provenance).

**exists** — all 26 named physical IO tags of the register's inventory (`DI1_SYS_ControlHealthy`
… `DQ11_SHR_FaultReset`, re-verified this run), CPU bits (`AlwaysTrue`, `FirstScan`), spares;
DBs `DB_Input`, `DB_Output`, `DB_AnalogInput` (+`ShredderMotorCurrent`), `DB_Controls` (all 5
members), `DB_Settings` (all 22 members as listed in the register), `DB_Alarms` (the DB; this
design's category-word names are proposed).

**proposed — new blocks/types this design defines** (hard rule 3: nothing is coded until gate-1
approval, and nothing references a proposed *field* tag at all): `OB100`-content,
`FC_InputMap`, `FC_OutputMap`, `FC_StartupReset`, `FC_Simulation`, `FC_EStopAlarms`,
`FC_GeneralAlarms`, `FC_PusherAlarms`, `FB_MotorDOL` (+UDT, 3 iDBs), `FB_MotorFwdRev` (+UDT,
iDB), `UDT_ShredderSeq`/`UDT_Pusher` member sets as specified in §2, `DB_PLC`, `DB_Timers`,
`DB_Alarms` word members, `DB_Settings.MachineType` + per-type table, buffer-member PascalCase
renames (AQ-01), `iDB_ShredderSequencer_SYS` / `iDB_PusherControl_PSH` naming.

**proposed — field/hardware gaps the engineer resolves** (from the register, re-confirmed
absent): remote pusher-cycle pushbutton DI (REQ-036/Q-03); machine-type selection (Q-04);
second-motor instrumentation (Q-05); AI channel for `ShredderMotorCurrent` (Q-11); outside-panel
reversal display + flashing fault lamp if PLC-driven (Q-13); zeroable-hours value/reset (Q-12).

## 10. Open questions

**Carried from the register** (never resolved here; one line each on what this design does
meanwhile): Q-01 restart behavior — design restarts to idle, faults preserved (§3), owner
statement owed · Q-02 nine unconfigured settings — homes assigned in §2, values owed (REQ-047's
5 s recorded as REQ-derived default) · Q-03 local/remote semantics — `LocalRemote` consumed,
`RemoteCycleCmd` reserved, wiring blocked · Q-04 machine-type structure — REQ-018/019/064/067
BLOCKED · Q-05 motor count — per-instance design scales, second instance blocked · Q-06 start
press-vs-hold — designed momentary-edge (FuncDesc primary), flip at coding if ruled otherwise ·
Q-07 upstream delay 3 s vs 6 s — designed 3 s (FuncDesc primary) via IFC `EnableUPSTime` ·
Q-08 downstream-recovery restart — designed no-auto-restart pending ruling · Q-09 spin-down home
— designed per-instance `ReverseDelay` (the register's own lean) · Q-10 overload annunciation —
REQ-059 BLOCKED · Q-11 AI hardware — overcurrent designed, arming blocked · Q-12 zeroable clock
— REQ-066 BLOCKED · Q-13 outside display/lamp — REQ-060 partial · Q-14 control-on step — not
designed in (FuncDesc primary); DQ11 wired as reset pass-through (AQ-05) · Q-15 hand-mode
structure — jog designed as an overlay gated from step 0; selection semantics owed.

**NEW (this design's own — AQ numbering to avoid colliding with the register's Q-nn; folding
them into the register/RFI belongs to the analysis stages):**

- **AQ-01 — settings re-homing sign-off.** C-307 (sharpened 2026-07-16, after the corpus was
  built) puts single-owner settings in the owning instance's UDT. This design homes every
  existing `DB_Settings` member accordingly (§2) and applies C-001 PascalCase to buffer members.
  Both change the HMI faceplate/binding surface — owner sign-off required before the migration
  is coded.
- **AQ-02 — `FC_Simulation` demo scope.** C-305/C-111 mandate the flag and the mapping-layer
  structure; the simulation driver FC itself is convention-mandated but non-trivial. Build now,
  or record an explicit owner waiver for the demo panel?
- **AQ-03 — fwd/rev motor: import vs freeform.** `MotorFwdRevSystem` exists at the site
  (named by `patterns/motor-dol/pattern.md`). C-108 prefers the proven block. Import and adapt,
  or build `FB_MotorFwdRev` freeform to this manifest?
- **AQ-04 — alarm category split.** Three category words (EStop/General/Pusher) per C-501/C-502.
  Confirm or re-cut at the `gen-alarm-design` stage.
- **AQ-05 — soft-start reset pass-through.** `DB_Output.MotorFaultReset` driven momentarily from
  `DB_Controls.FaultReset` — confirm this is the intended use of DQ11 (relates Q-14).

---

## Gate-1 sign-off (docs/15 hard gate 1 — before any block is coded)

This manifest is a proposal. Freeform share ~78% — signing this is also the explicit
freeform go-ahead of CLAUDE.md workflow step 3, or the place to refuse it.

- [ ] Approved to proceed to `gen-alarm-design` / `gen-block-coding` — Engineer: ____________
  Date: ____________
- Scope notes / exclusions at sign-off: ____________

## Change manifests

### 2026-07-16 — Functional fix wave 1 (owner rulings of 2026-07-16; phase 1, portal-free)

**Touched blocks** — modified only, nothing created or deleted: `FB_ShredderSequencer` (N1/N9/
N14 + header), `FB_PusherControl` (N1/N2/new N4/N5–N8/N10/N11/N13 + header), `FC_ControlMain`
(N1/N3/N5 + new header comment — standing C-201 finding closed in passing since the block was
being edited), `DB_Settings` (five start values + four documented-unconfigured member comments),
`iDB_MotorFwdRevSystem_Shredder` (**start values only**: `FTTime` 10.0, `ReverseIgnoreFT` 12.0 —
imported-real, logic untouched), `iDB_ShredderSequencer`/`iDB_PusherControl` (interface-copy
mirrors). **Interface changes: YES** — `UDT_ShredderSequencerIO` gains `SystemHealthy`,
`ShredderRunRevFB` (in) and `MotorStartArm`, `PusherParkCmd` (out); `UDT_PusherIO` gains
`ParkCmd` (in); `FB_PusherControl` gains Statics `CycleRequest`/`JogDemand`/`ReparkRequest`/
`LaunchRequest` (C-601 named conditions). All new UDT members carry C-605 comments.
**Pattern or freeform:** freeform edits to already-freeform blocks (no pattern covers them);
authorization is the owner's four explicit rulings themselves — this wave implements direction,
it does not originate design. **REQ refs:** REQ-005/006 (reverse run on confirmed feedback),
REQ-012/013/062 (stop bundle: sequencer `SystemHealthy` in `StopCmd`, StopCmd forces pusher mode
0, `RecentStart` strap replaced by `MotorStartArm` press-to-arm), REQ-039/041/043/044/045 +
NEW-4 (repark only on pre-start `PusherParkCmd` or cycle entry, all rest-to-motion starts warned
via the generalized pre-motion warning, `Fitted` gating at the source), REQ-004/007/031/047/054/
055/063 (timing defaults — PROPOSALS, gated on `gen/GenProject1/fix-wave-1.md` §1 sign-off).
**Tag status:** every referenced tag exists (`DB_Input.Control_Healthy`,
`DB_Input.Shredder_Run_Rev_FB` grep-verified; no proposed field tag referenced). This section
8's cross-instance wiring plan gains three rows in reality: `DB_Input.Control_Healthy` →
SEQ.`SystemHealthy`; `DB_Input.Shredder_Run_Rev_FB` → SEQ.`ShredderRunRevFB`;
SEQ.`MotorStartArm` → motor `RecentStart` (plain-coil set, FB self-clears);
SEQ.`PusherParkCmd` → pusher `ParkCmd`.
**Gate:** phase 2 (import → compile → invariance → reviewers → presentation) waits on the
settings sign-off block in `gen/GenProject1/fix-wave-1.md` §1 — pending until the engineer
signs. — Sign-off: pending.

### 2026-07-17 — C-115 handshake vocabulary (owner-questions C-5, `agent-tasks/09-sequencer-
interface-extension.md`)

**Touched blocks** — modified only, nothing created or deleted: `UDT_PusherIO`, `UDT_ShredderSequencerIO`
(interface changes), `FB_ShredderSequencer` (Network 15 — `IO.InCycle` wired), `FB_PusherControl`
(interface reflection only, no logic change). **Interface changes: YES.** `AutoStartSignal : Bool`
(enable in, genuinely new) added to both UDTs. `UDT_PusherIO.Cycling` and
`UDT_ShredderSequencerIO.EnableUpstream`/`InCycle` recognized under comment as already serving the
running/ready/running roles — not duplicated under generic names.
**Two conflicting sign-offs, reconciled:** this gate ran twice, independently, in two concurrent
sessions, and got two different answers from the owner without either side knowing. Resolution (a)
(three brand-new members: `AutoStartSignal`/`UPSEnable`/`Run`) was built and compiled first;
resolution 2 (reuse existing members, add only `AutoStartSignal`) was drafted independently and
left incomplete (blocked on the D-6 converter gap wiring `InCycle`). Owner's final word, on being
shown the conflict: **"switch to reuse please that was my mistake."** Resolution 2 is what's built;
resolution (a) was fully reverted. **Pattern or freeform:** interface-only, no pattern applies.
**REQ refs:** none directly — this is a doc-06 rule application (C-115), not requirement-driven.
**Tag status:** `AutoStartSignal` is `proposed` in both UDTs (new, `FC_ControlMain` doesn't wire
it); every reused member (`Cycling`, `EnableUpstream`, `InCycle`) already `exists`.
**Gate:** mini-manifest presented and signed off before coding (docs/15 hard gate 1) — see the
conflict note above; the owner's final answer is authoritative. — Sign-off: **received** (2026-07-17,
"switch to reuse please that was my mistake").
**Compile evidence:** whole-device 0 errors/0 warnings; both `--type` compiles clean; Import()
cascade to `FB_ShredderSequencer`/`FB_PusherControl`/their iDBs/`FC_ControlMain`/`OB100`/
`FC_AlarmsMain` cleared via block-level compile; re-export byte-identical against authored UDTs;
`FB_ShredderSequencer` diff against pre-edit `HEAD` showed exactly the intended two changes
(dropped stale `UPSEnable`/`Run` reflection from the reverted resolution (a), added the `InCycle`
wiring) — task 06's re-arm fix and everything else confirmed untouched.
