# Architecture manifest — MotorStarter DOL → VSD purpose change

Gate-1 design artifact (docs/15 `gen-architecture`). **Tier-(c) purpose change** of a single
existing FB (`gen-block-modify-purpose`, S7). No logic written at this stage — this is a proposal
the owner signs before any coding. Blind-validation run: designed only from the three
generator-visible inputs; the quarantined answer key was never accessed.

## Provenance

- **Date:** 2026-07-20
- **Inputs consumed:**
  - `gen/_validation/MotorVSDSystem-purpose/spec.md` — REQ register (REQ-C1…C14, REQ-V1…V8) — git `a00d762`
  - `gen/_validation/MotorVSDSystem-purpose/source/MotorStarter.ir` — DOL baseline, 14 networks — git `a00d762`
  - `gen/_validation/MotorVSDSystem-purpose/deps/MotorVSDIOSet.ir` — target IO UDT — git `a00d762`
  - `gen/_validation/MotorVSDSystem-purpose/deps/AnalogScale.ir` — scaling FC to CALL (REQ-V3) — git `a00d762`
- **Designed without** `requirements.md`/`process-topology.md`/`io-map.md`/`rfi.md`: the spec IS the
  numbered register for this validation. Consequences: no physical addressing enters the design
  (buffer/interface members only); material-flow direction is not in scope (single-block modify).
- **Independence:** independent of the answer key by construction (never read). Design derived from
  the register + DOL source + dep types only.
- **Convention basis:** `docs/06` preamble (function → readability → efficiency; stricter bar for
  generated code), C-113/C-115/C-118/C-126/C-127, C-001/C-003, C-604/C-606.

## 1. Block manifest

| Item | Kind | Action | Purpose |
|------|------|--------|---------|
| `MotorStarter` | FB (NUMBER 2, LAD) | **modify (purpose)** | Repurpose DOL motor control → VSD motor control: layer speed reference on top, swap single-contactor feedback for bidirectional drive feedback |
| `MotorVSDIOSet` | UDT | referenced (retype target) | New IO struct the FB's `IO` member is retyped to; caller-facing signal set (provided, not defined here) |
| `AnalogScale` | FC (NUMBER 28) | CALL only (REQ-V3) | Reusable linear scaling `0–100 % → 0…MaxRPM` |
| `iDB_MotorStarter_*` | instance DB | unchanged shape, re-typed by retype | Follows the IO retype automatically; not edited here |

**FB stays FB (C-113 memory test / C-118):** the block already holds cross-scan Static state
(constructed edge flags, S/R latches, TON/TONR instances) and the VSD purpose ADDS more remembered
state (startup timer, last-speed value, reverse-edge memory). It must remember its phase → FB by
construction. No block-kind change. **Name preserved** (`MotorStarter`, import-by-name identity);
a rename to `MotorVSDSystem` is an optional owner decision, not required by any REQ (see Open questions).

## 2. Interface delta (spec Section C)

**Retype** `IO : "MotorIOSet"` → `IO : "MotorVSDIOSet"` (RETAIN SETPOINT unchanged).

**Removed** (DOL had, VSD does not — every consumer must be rewired, else it won't compile):

| Member | DOL consumers that must change |
|--------|--------------------------------|
| `RunningFB` | NW7, NW8, NW9, NW11, NW12, NW13 |
| `FaultFB` | NW10 (aggregation + reset coil), NW14 (alarm bit) |

**Added, driven/consumed by this FB** (all present in `MotorVSDIOSet`, verified — see §9):
`RunRev`, `RunningFwdFB`, `RunningRevFB`, `VSDError`, `HandSpeedInput`, `AutoSpeedInput`,
`SpeedPerc`, `SpeedOutput`, `MaxRPM`, `StartUpTime`.

**Added, present on the UDT but NOT driven/consumed here** (non-requirements — C-606 no-gold-plating;
the design deliberately ignores them): `VSDWarning`, `SpeedFB`, `SpeedReached`, `AmpsFB`,
`OverTempFault`, `ThermalOLFault`, `TorqueLimitOk`, `VSDReady`, `VSDOpEnable`, `VSDOverSpeed`,
`VSDUnderSpeed`, `RotationSensor`.

**New internal (private) FB state** — generator's own declarations, not caller-facing (all `proposed`):

| Declaration | Type | For |
|-------------|------|-----|
| `StartUpTimer` | `TON_TIME` instance | REQ-V4 startup / speed-settle window |
| `StartUpTimeMS` | `DInt` | REQ-V4 scaled ms preset (computed in NW6 alongside C-6 times) |
| `LastSpeed` | `Real` | REQ-V4 setpoint-stable comparison (previous-scan `SpeedPerc`) |
| reverse-edge memory bit(s) | `Bool` (or new array slot) | REQ-V7 `RunRev` edge tracking, constructed-edge idiom per C-4 |

**C-115 handshake conformance:** the shared vocabulary the DOL exposed is retained verbatim on
`MotorVSDIOSet` (`InHand`, `AutoStartSignal`, `HandStartSignal`, `Run`, `FaultActive`, `FaultReset`,
`FTR`, `FTS`, `UPSEnable`, `Telemetry`, `Alarm`, `Shutdown`, `StopMotor`, …). The additions follow
the same naming idiom (`*FB` feedback, `VSD*` drive status, `Speed*` reference) — no deviation from
the site handshake, only an extension of it.

## 3. Network plan against the DOL's 14 networks — the invariance boundary

Classification key: **UNCHANGED** = must be byte-identical post-change (the `converter diff --only`
invariance set the build proves untouched); **MODIFIED** = rewired; **ADDED** = new.

| DOL NW | Title | Class | REQ | What changes / why identical |
|--------|-------|-------|-----|------------------------------|
| 1 | DOL Motor Start / Stop | **UNCHANGED** | C-1 | No feedback/fault refs; run-permission already blocks on FTR via `FaultActive`. Byte-identical. |
| 2 | Hand Intervention Logic | **UNCHANGED** | C-2 | HMI Set/Reset latch; no touched members. Byte-identical. |
| 3 | Start Signal After Inhibit/Fault | **UNCHANGED** | C-3 | Constructed debounce timer; no touched members. Byte-identical. |
| 4 | Hand Selector Edges And Prestart Timeout | **UNCHANGED** | C-4 | Constructed edges + prestart timeout; no touched members. Byte-identical. |
| 5 | Prestart Hold On Until Motor Run Signal | **UNCHANGED** | C-5 | `PreStartMemory` latch; no touched members. Byte-identical. |
| 6 | HMI Times | **MODIFIED** | C-6, V-4 | Add one MUL+CONVERT pair `StartUpTime → StartUpTimeMS` into the existing up-front scaling block (C-126 documented single-place idiom; the NW comment already anchors this). |
| 7 | DOL Motor Shutdown | **MODIFIED** | C-7, V-6 | `NOT IO.RunningFB` → derived not-running (`NOT (RunningFwdFB OR RunningRevFB)`). Behaviour preserved; feedback source only. |
| 8 | Fail To Run | **MODIFIED** | C-8, V-5, V-6 | Two changes: arm the fail timer off **startup-window completion** (`StartUpTimer.Q`) instead of directly off `Run` (V-5); feedback `RunningFB` → direction-appropriate running (V-6). Genuine behavioural rewire. |
| 9 | Fail To Stop | **MODIFIED** | C-9, V-6 | `IO.RunningFB` → derived running. Feedback source only. |
| 10 | Faults | **MODIFIED** | C-10, V-8 | `IO.FaultFB` → `IO.VSDError` in aggregation; **drop the `RCOIL IO.FaultFB := FaultReset` line** — `VSDError` is a drive-provided input, not computed/reset here. |
| 11 | Motor Running, Enable Upstream | **MODIFIED** | C-11, V-6 | `IO.RunningFB` → derived running. Feedback source only. |
| 12 | Hours Run Counter | **MODIFIED** | C-12, V-6 | `IO.RunningFB` → derived running. Feedback source only. |
| 13 | HMI Motor Status Telemetry | **MODIFIED** | C-13, V-6 | `IO.RunningFB` (×3 EN conditions) → derived running. Feedback source only. |
| 14 | Alarm | **MODIFIED** | C-14, V-8 | `%X2 := IO.FaultFB` → `%X2 := IO.VSDError`. Third alarm source swap only. |
| — | **VSD Speed Reference** | **ADDED** | V-1, V-2, V-3 | Select `SpeedPerc` from `HandSpeedInput`/`AutoSpeedInput` by hand/auto (V-1); floor each input at configured min (V-2); `CALL AnalogScale` `0–100 → 0…MaxRPM` → `SpeedOutput` (V-3). |
| — | **VSD Startup / Speed-Settle Window** | **ADDED** | V-4, V-7 | `StartUpTimer` runs only while running AND setpoint settled (`SpeedPerc ≈ LastSpeed`) AND no recent direction change; `RunRev` edge (V-7) re-arms the window. `.Q` arms NW8 (V-5). |

**Invariance boundary (build must prove with `converter diff --only 1 2 3 4 5`):** NW1, NW2, NW3,
NW4, NW5 stay byte-identical. All others are in the changed set.

**Derived "running" condition (REQ-V6):** the running/not-running signal that replaced `RunningFB`
= direction-appropriate feedback — forward command consumes `RunningFwdFB`, reverse consumes
`RunningRevFB`; aggregate running = `RunningFwdFB OR RunningRevFB`. Exact expression is the coder's
(spec keeps permissives behavioural); the design fixes only the source substitution.

**Added-network count/ordering:** 2 added networks as planned; the coder may split the speed
reference into two (selection+clamp / scaling CALL) for readability — count and titles are the
coder's derivation (spec blindness note). Data dependency: the startup-window network must sit
**before** NW8 so `StartUpTimer.Q` is current when NW8 reads it.

## 4. REQ → network traceability

| REQ | Network(s) | REQ | Network(s) |
|-----|-----------|-----|-----------|
| C-1 | NW1 (unchanged) | C-11 | NW11 (mod, feedback) |
| C-2 | NW2 (unchanged) | C-12 | NW12 (mod, feedback) |
| C-3 | NW3 (unchanged) | C-13 | NW13 (mod, feedback) |
| C-4 | NW4 (unchanged) | C-14 | NW14 (mod, VSDError) |
| C-5 | NW5 (unchanged) | V-1 | ADDED speed-reference |
| C-6 | NW6 (mod, +StartUpTime) | V-2 | ADDED speed-reference (min clamp) |
| C-7 | NW7 (mod, feedback) | V-3 | ADDED speed-reference (AnalogScale CALL) |
| C-8 | NW8 (mod, V-5 arm + feedback) | V-4 | ADDED startup-window + NW6 (ms scaling) |
| C-9 | NW9 (mod, feedback) | V-5 | NW8 (arm from StartUpTimer.Q) |
| C-10 | NW10 (mod, VSDError) | V-6 | NW7, NW8, NW9, NW11, NW12, NW13 |
| | | V-7 | ADDED startup-window (RunRev edge re-arm) |
| | | V-8 | NW10, NW14 |

Every REQ-C1…C14 and REQ-V1…V8 mapped. No unmapped REQ.

## 5. Carving tier & scope judgement — tier-(c), stays in modify-purpose

**Tier (c) — modified library block.** The DOL FB covers the whole carried-over behaviour set
(C-1…C-14) as its existing skeleton; the VSD change is a **scoped, stated deviation** (feedback-model
swap + layered speed control) — exactly the modify-purpose case, not tier-(a) whole-reuse and not
tier-(d) rewrite.

**Changed-vs-preserved split (14 existing networks + 2 new):**

- **Preserved byte-identical:** 5 (NW1–5) — the invariance boundary.
- **Modified:** 9 — of which **7 are single-source feedback/fault substitutions** (`RunningFB` /
  `FaultFB` → drive feedback) that preserve each network's behaviour and structure; **1** is a
  shared-time-scaling extension (NW6, one MUL+CONVERT pair, C-126 idiom); **1** is a genuine
  behavioural re-wire (NW8, V-5 arming).
- **Added:** 2 (speed reference; startup window).

**Does NOT trip the "touches nearly the whole block → route to gen-block-new" guardrail.** Touched
count is high (11/14) but the *behavioural surface* is small: 12 of 14 network behaviours are
unchanged (5 identical + 7 mechanical feedback substitutions), only NW8 is behaviourally rewired and
NW6 extended, plus 2 additive networks. The block's identity, structure, ordering, and the large
majority of its behaviour are preserved — the DOL skeleton IS the VSD skeleton plus a speed layer.
This is a scoped purpose change → **stays in `gen-block-modify-purpose`.**

## 6. Pattern mapping / freeform

No `patterns/` composition — this is an in-place modify of one proven site block (tier-(c)); the
base block is the reuse. The added content:

- Speed-reference network: structured by REQ-V1/V2/V3 + a library `CALL AnalogScale` (proven FC) — not freeform-shaped; a MOVE-cascade select + clamp + FC call.
- Startup-window network: a TON with a constructed re-arm, same edge/S-R idioms the DOL already uses (C-4 constructed edges) — convention-structured, not novel.

**Freeform %:** 2 added networks of 16 planned ≈ **13%** (planned-networks basis), and both added
networks are convention/library-structured rather than truly freeform. **Under the 20% gate** — no
freeform go-ahead flag raised; gate-1 sign-off does not need to double as a CLAUDE.md step-3 waiver.

## 7. DB landscape / OB100 / enable-chain / OB1 order

Out of scope for a single-block purpose change and **inherited unchanged** from the host project:

- No DB created/repacked here; the instance DB re-types automatically with the IO retype.
- OB100/`DB_PLC` startup machinery (C-305/C-403) **not touched** by this change — inherited from the
  existing block context, neither introduced nor waived here.
- No enable-chain / command-flow graph and no OB1 call-order change: this modifies one FB's internals;
  its call site and neighbours are unchanged.

## 8. Cross-instance wiring (C-127)

**None.** All state and I/O are within this single FB and its own `IO` UDT instance; no sibling-instance
names appear in its logic. `AnalogScale` is a stateless FC call with bare params (`Input`, `Input_Min`,
`Input_Max`, `Scaled_Min`, `Scaled_Max`, `Output`) — no cross-instance fact. C-127 clean.

## 9. Tag status

Corpus for this validation = the provided dep types (`MotorVSDIOSet`, `AnalogScale`); no
`ir/<project>/` export. Verified against `deps/MotorVSDIOSet.ir` (git `a00d762`) at write time:

**`exists`** (members of `MotorVSDIOSet`, grep-verified): `InHand`, `AutoStartSignal`,
`HandStartSignal`, `RunningFwdFB`, `RunningRevFB`, `VSDError`, `SystemHealthy`, `PreStartDone`,
`InhibitMotor`, `FaultReset`, `FTTime`, `EnableUPSTime`, `FaultActive`, `Run`, `RunRev`, `UPSEnable`,
`FTR`, `FTS`, `HrsRun`, `Telemetry`, `Alarm`, `HandIntervention`, `TryRunMotor`, `RecentStart`,
`ShutdownComplete`, `ShutdownTime`, `Shutdown`, `StopMotor`, `HandSpeedInput`, `AutoSpeedInput`,
`SpeedOutput`, `SpeedPerc`, `StartUpTime`, `MaxRPM`. `AnalogScale` FC (NUMBER 28) exists.

**`proposed`** (must be resolved before/at coding — hard rule 3):

| Proposed | Kind | Note |
|----------|------|------|
| `StartUpTimer`, `StartUpTimeMS`, `LastSpeed`, reverse-edge bit(s) | private FB state | New internal declarations the design defines; `proposed` until a fresh export shows them. |
| **minimum-speed setpoint (REQ-V2)** | value with **no home** | REQ-V2 needs a "configured minimum" to floor speed inputs; `MotorVSDIOSet` has **no such member**. Owner decision required (see Q-NEW-2) — do not invent it. |

## 10. Open questions

- **Q-NEW-1 (owner) — who writes `RunRev`?** No REQ derives the reverse run command. REQ-V6 says the
  drive "exposes" `Run`/`RunRev`; REQ-V7 tracks `RunRev` edges. The design assumes `RunRev` is a
  caller/HMI-provided command input (like `AutoStartSignal`) — consumed for edge re-arm (V-7) and
  direction-appropriate feedback (V-6), **not computed by this FB**. If this FB is expected to
  arbitrate/command reversal, a REQ is missing. Confirm.
- **Q-NEW-2 (owner) — home for REQ-V2 minimum speed.** No `MotorVSDIOSet` member exists for the
  configured minimum. Options: (a) add an HMI-settable UDT member (C-307 single-owner setting), or
  (b) a fixed private constant (not operator-tunable). Design cannot pick silently — awaiting owner.
- **Q-NEW-3 (owner, minor) — rename `MotorStarter` → `MotorVSDSystem`?** Not required by any REQ; keeping
  the name preserves import-by-name identity. Rename is a separate, deliberate action if wanted.

## Gate-1 sign-off — SIGNED (owner, 2026-07-20)

Manifest approved. Coding authorized. Open questions resolved by the owner at sign-off:

- **Q-NEW-1 resolved:** `RunRev` is a **caller-provided command input** — this FB consumes it
  (edge re-arm V-7, direction-appropriate feedback V-6); it does **not** compute/arbitrate it.
- **Q-NEW-2 resolved:** add a **new HMI-settable UDT member `MinSpeed : Real`** to `MotorVSDIOSet`
  (C-307 single-owner setting) as REQ-V2's minimum-speed home. REQ-V2 floors each speed input at
  `IO.MinSpeed`. **Not** an FB private constant.
- **Q-NEW-3 resolved:** **RENAME the block `MotorStarter` → `MotorVSDSystem`.**

- **Reviewed / approved (engineer):** owner  **Date:** 2026-07-20  *(SIGNED)*

## Build refinement (as-built, 2026-07-20)

Delivered block `MotorVSDSystem.ir`. The build kept the plan's classification exactly (NW1-5 invariant,
NW6-14 modified in place, additions at the end so NW1-14 numbering is stable) but refined the
**added-network count from the manifest's "2 (may split to 3)" to 5**, for three build-time reasons:

- **A shared `MotorRunning` helper bit (NW18)** — the REQ-V6 derived running (`RunningFwdFB OR
  RunningRevFB`) is computed once and read by NW7/8/9/11/12/13, so each feedback swap is a clean
  single-token change and no consumer chain ends up with two OR-groups (synthesizer rule).
- **The AnalogScale range convert is its own network (NW15)** — statement kind-order puts a CALL
  *before* a CONVERT, so the `MaxRPM` Int→Real convert that feeds the CALL cannot share the CALL's
  network; it precedes it. Speed-output Real→Int is likewise its own network (NW17) after the CALL.
- Net added networks: NW15 Max-Speed Range, NW16 Speed Reference (CALL), NW17 Speed Output, NW18
  Running State, NW19 Startup/Speed-Settle Window. Modified: NW6-14 (9). Invariant: NW1-5 (proven).

`StartUpTimer.Q` is read one network *before* it is written (NW8 reads it, NW19 writes it) — a
one-scan lag, consistent with the block's existing cross-network idiom (NW1 reads `PreStartMemory`
written in NW5). Same for `MotorRunning`. Negligible against second-scale timers.
