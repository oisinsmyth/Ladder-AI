# test-project001 — Hopper-Blockage Alarm — Architecture Manifest (S6 request #1)

Gate-1 design manifest for the one FB carved from `hopper-blockage-alarm/requirements.md`. Produced
by the `gen-architecture` skill (docs/15 stage 5). **Design only — no logic written, no compile, no
Portal contact.** Proposal until the gate-1 line at the bottom is signed.

Scoping: separate file in the request folder (baseline `gen/test-project001/architecture.md`
untouched).

> **CHANGED FROM v1 AT OWNER GATE-1 (2026-07-19).** Gate 1 was signed (build it; 100% freeform
> granted; inhibit-as-output-demand confirmed). Owner overrode two v1 recommendations:
> **NEW-HBA-01 → pause-and-resume** (not reset); **NEW-HBA-02 → corpus UDT-IO style** (not plain
> sections). NEW-HBA-03 (`T#2S`) confirmed. Reconciling those two overrides surfaced a **material
> correction to v1's C-128 reasoning** (see §3) and a **C-406 timer-mechanism decision** (see §2/§6),
> both raised as second-confirm items above the sign-off.

## Provenance

- **Date:** 2026-07-19. Stage 2 (Design), revision 2 (post-gate-1-override).
- **Inputs consumed:**
  - `gen/test-project001/hopper-blockage-alarm/requirements.md` — git `untracked` (uncommitted).
  - `ir/test-project001/` export — corpus commit `c8c3dea` (HEAD); tag-status greps re-run this run.
  - `docs/06-lad-conventions.md` — read fresh this run (C-124/C-128/C-403/C-406/C-407/C-115 rule
    text quoted where load-bearing below).
  - `ir/test-project001/OB100.ir`, `iDB_ShredderSequencer.ir`, `FB_MotorFwdRevSystem.ir` — read for
    the startup-reset and retentivity convention (the C-124 carve-out, the TONR precedent).
  - `patterns/` — none matches a persistence/debounce alarm monitor (tier-(d), unchanged from v1).
- **Designed without `process-topology.md`, `io-map.md`, `rfi.md`** (register-only — supported).
  Immaterial here (single supervisory FB, no enable chain, no physical IO read directly).
- **Independence:** greenfield new block. Corpus files read only for convention (C-115 vocabulary,
  C-124 startup carve-out, retentivity) — informed on conventions, no as-built hopper logic exists.

## 1. Block manifest

| Block | Kind | Purpose | Tier | Status |
|---|---|---|---|---|
| `UDT_HopperBlockageIO` | UDT | C-115 handshake interface for the monitor FB | — | proposed (NEW, additive) |
| `FB_HopperBlockageMonitor` | FB (LAD) | Supervise `Hopper_Level_High`: raise a latched hopper-blockage alarm + a stop/inhibit demand when the hopper is (debounced-)high for a cumulative threshold while the plant runs | (d) freeform | proposed |
| `iDB_HopperBlockageMonitor` | Instance DB | Single instance of the above | — | proposed |

**Scope assessment (UDT creation).** All three blocks are **additive/new** — no existing block is
edited. `converter diff` stays clean on `FC_ControlMain`/`OB100`/`DB_*`. Creating the FB's own
dedicated interface UDT is the corpus's own FB+UDT+iDB equipment unit (C-115/C-503), not an
existing-block edit → **assessed in-scope for `gen-block-new`** (import order: UDT first, then FB,
then iDB — per the project's UDT-first import convention). This differs from v1 (which used plain
sections and no UDT); the switch is the owner's NEW-HBA-02 override.

**FB-vs-FC (C-113 memory test):** must remember — holds timer instances, a cumulative accumulator,
and a latched alarm across scans → FB by construction (C-118 memory principle). Single instance.

**No OB/FC/shared-DB is modified by this item.** All wiring to globals (in and out) is the separate
integration step (§8), out of scope, enforced by `converter diff`.

## 2. Interface definition (corpus UDT-IO style — NEW-HBA-02 override)

`FB_HopperBlockageMonitor` carries one `IO : "UDT_HopperBlockageIO" RETAIN SETPOINT` (the corpus
pattern — matches `FB_PusherControl.IO`, `iDB_ShredderSequencer.IO`). Transient internal state lives
in **non-retentive Static outside the UDT** (matches the corpus, where edge-memory/counters sit in
Static, separate from the RETAIN IO UDT).

**`UDT_HopperBlockageIO` members:**

| Member | Type | Role | Retentivity note |
|---|---|---|---|
| `HopperLevelHigh` | Bool | In — blockage sensor (integration wires `DB_Input.Hopper_Level_High`) | re-driven each scan; retain immaterial |
| `PlantRunning` | Bool | In — run-state gate; window accrues only while true (Q-HBA-04). Integration wires `Shredder_Run_Fwd_FB OR Shredder_Run_Rev_FB` | re-driven each scan |
| `FaultReset` | Bool | In — operator reset (integration wires `DB_Controls.FaultReset`). C-115 vocabulary, verbatim | re-driven each scan |
| `BlockedTimeThreshold` | Time | Setting — cumulative persistence threshold, start value `T#60S` (Q-HBA-01). FB-own tunable, not `DB_Settings` | RETAIN SETPOINT — persists (C-307/C-309) |
| `ClearDebounceTime` | Time | Setting — confirmed-clear filter, start value `T#2S` (Q-HBA-02, confirmed NEW-HBA-03) | RETAIN SETPOINT — persists |
| `HopperBlockedAlarm` | Bool | Out — latched blockage alarm (REQ-HBA-001/004) | **RETAIN — survives power cycle (C-124 carve-out; see §3)** |
| `HopperBlockStopReq` | Bool | Out — stop/inhibit **demand** (Q-HBA-05); no shared write here | **RETAIN — tracks the latch, survives power cycle** |

**C-115 handshake vocabulary — NEW-HBA-07 (minor).** The owner's 2026-07-17 C-115 ruling ("expose
enable/ready/running always, just ignore when not needed") nominally adds `Enable` in / `Ready`,
`Running` out. For a pure *supervisory monitor* (no motion, no enable-chain participation) these
have no clear meaning and inventing semantics risks C-606 gold-plating. **Question:** add unwired
C-115 stubs, or document a supervisory-monitor exemption? Recommend the documented exemption. (Input
gate renamed `PlantRunning`, not `Running`, to avoid colliding with C-115's `Running` output slot
either way.)

**C-307/C-308 settings:** both `Time` tunables are single-owner, this-instance-only → homed in the
FB's own interface as start values (C-307 per-instance shape, like the motor FB's `ReverseDelay`),
**not** `DB_Settings`. HMI/commissioning writes, logic reads. No scan-copy from any shared DB (C-308
trap — structurally impossible; no `DB_Settings` hopper member exists to copy).

**Internal Static (non-retentive, outside the UDT):**

| Member | Type | Role |
|---|---|---|
| `PersistenceTimer` | TON_TIME | Times each contiguous high-and-running segment (TON, **not TONR** — C-406; see §6) |
| `ClearDebounceTimer` | TON_TIME | Times `NOT HopperLevelHigh` for the confirmed-clear filter (TON) |
| `PersistenceAccumulator` | Time (or DInt ms) | Cumulative high-and-running elapsed across pause/resume segments |
| `HighQualified` | Bool | Debounced blockage-present latch: set on high, cleared only on confirmed-clear (holds through momentary clears) |
| edge-memory bit(s) | Bool | C-404 edge construction for accumulating `PersistenceTimer.ET` on segment end |

## 3. DB landscape + startup/retentivity (v1 C-128 reasoning CORRECTED)

Adds one DB: `iDB_HopperBlockageMonitor`. Touches no buffer DB, `DB_PLC`, `DB_Controls`,
`DB_Settings`, `DB_Alarms`, `DB_Timers`, or `OB100`.

**Correction to v1.** v1 read C-128 as "no latched fault may survive a power cycle → make the alarm
non-retentive." **That was wrong.** The actual rule text:
- **C-128** (error): "*No automatic restart after a stop or power event … it never resumes
  **motion** on its own.*" C-128 governs **motion restart**, not fault-latch clearing.
- **C-124** (error): on OB100 restart, transient run-state force-clears — but "*a genuine fault
  latch … already `RETAIN` is **deliberately excluded** — a fault needing human acknowledgement
  still needs it after a power cycle.*"
- **Corpus precedent (grounded):** `iDB_ShredderSequencer.IO.DischargeConveyorTimeoutFault` — the
  closest sibling to this alarm — lives in a `RETAIN SETPOINT` UDT-IO and is **explicitly listed in
  OB100's header as excluded from startup clearing**. It survives the power cycle; only `FaultReset`
  clears it.

**Therefore:** `HopperBlockedAlarm` is a genuine `FaultReset`-cleared fault latch → **RETAIN, and it
survives the power cycle by design** (C-124 carve-out). This is *fail-safe*: on power-up a
pre-existing blockage alarm stays asserted (and `HopperBlockStopReq` stays demanding a stop) until a
human acknowledges — the opposite of the auto-restart hazard C-128 guards against. **No OB100 edit
and no first-scan self-clear is needed** for the latch → the RETAIN UDT-IO the owner requested is
correct, and the whole item stays in `gen-block-new` scope.

**This conflicts with REQ-HBA-007 as written** ("*no … latched alarm survives the power cycle*").
That clause was derived from v1's C-128 misreading → **NEW-HBA-04**: revise REQ-HBA-007 so the alarm
latch survives per the C-124 carve-out (aligning with the `DischargeConveyorTimeoutFault` sibling).
Requires owner OK — it changes a signed register REQ.

**Transient state at power-up.** `PersistenceTimer`/`ClearDebounceTimer`/`PersistenceAccumulator`/
`HighQualified` are the "in-progress" transient run-state C-124 wants at idle after restart. Declared
**non-retentive**, they reset at power-up naturally — meeting C-124's *outcome* in-scope. C-124's
*letter* ("clear at OB100 regardless of retentivity", belt-and-suspenders from the C-403 incident)
would add an OB100 clear network → an OB100 edit, out of scope. **NEW-HBA-06 (minor):** rely on
non-retentivity (in-scope, recommended — this state is non-safety, it can only delay an alarm, never
command motion), or add the belt-and-suspenders OB100 clear (expanded scope). Recommend in-scope.

## 4. Enable-chain / command-flow graph

Not applicable (C-114/C-116). A single standing supervisory/interlock FB: commands no motion, sits
in no enable sequence. Its one outward edge (`HopperBlockStopReq`) is an interlock/status demand
(C-114 scope note — may run against flow, is not an enable). No cycle; acyclicity trivial.

## 5. OB1 call order

No OB1 change defined by this item. At integration, called after input mapping, before output
mapping (C-110), in `FC_ControlMain` with the other equipment-FB calls. No OB100 call-list change
(§3).

## 6. Pattern mapping + C-406 timer mechanism

| Manifest item | Tier | Basis |
|---|---|---|
| `FB_HopperBlockageMonitor` (+UDT, iDB) | **(d) freeform** | No `patterns/` entry covers a persistence/debounce level-blockage alarm; `motor-dol` is a DOL starter, unrelated |

**Freeform share: 100%** (basis: planned networks, 5 of 5). >20% → gate-1 sign-off **is** the
workflow-step-3 freeform go-ahead (already granted at v1 gate-1; re-affirmed for the +1 network).

**Pause-resume vs C-406 (NEW-HBA-05 — second confirm).** Owner chose pause-and-resume (NEW-HBA-01):
the cumulative high-and-running time must freeze on `PlantRunning` loss and resume, not reset. Two
mechanisms:
- **C-406 rule text is a blanket ban on TONR** (error): "*TON is the only timer instruction used …
  retentive behaviour [is] constructed explicitly from TON plus inversion/edge logic … rather than
  using TOF, TP, or TONR.*" Its action item explicitly names "runtime counters, duty totals" (i.e.
  cumulative timing) as the TONR use cases to rebuild on TON.
- The corpus **does** use `TONR_TIME` (`FB_MotorFwdRevSystem.HrTotaliserTimer`, cumulative run-hours,
  `RETAIN`) — a **pre-existing C-406 deviation**, routed to `/review-conventions` as an observation,
  not adjudicated here.

**Recommendation (i):** build cumulative timing from **TON + a non-retentive `PersistenceAccumulator`**
(C-406-compliant): `PersistenceTimer` times each contiguous high-and-running segment; on the segment's
falling edge (C-404 edge), add its `ET` (read as a value and copied per C-408, never compared
mid-rung) into the accumulator; trip when `accumulator + live ET ≥ BlockedTimeThreshold`; reset the
accumulator on confirmed-clear and `FaultReset`. More logic, honours the stricter generated-code bar.
**Alternative (ii):** a single `TONR` (`IN := high-and-running`, `R := confirmedClear OR FaultReset`)
— far simpler, has the `HrTotaliserTimer` precedent, but is a **C-406 error-rule exception needing a
documented owner OK**. Recommend (i); flag (ii) for the owner's call.

Freeform structuring rules: C-406 (TON), C-404/C-408 (explicit edges, ET-as-value), C-123 (latched
fault vs live hold), C-126 (one writer per output), C-601 (name-once), C-124 (transient state
non-retentive; latch RETAIN per carve-out), network titles.

## 7. REQ → block traceability

Networks: NW1 "Confirmed-clear debounce", NW2 "Blockage-present qualifier", NW3 "Cumulative
persistence (TON + accumulator)", NW4 "Hopper-blocked alarm latch", NW5 "Stop / inhibit demand".

| REQ / RFI | Implemented by | Network(s) |
|---|---|---|
| REQ-HBA-001 continuous(cumulative)-high threshold alarm | `FB_HopperBlockageMonitor` | NW2 + NW3 + NW4 |
| REQ-HBA-002 no alarm below threshold | `FB_HopperBlockageMonitor` | NW3 (accumulator not reaching threshold) |
| REQ-HBA-003 momentary clear re-arms (debounced) | `FB_HopperBlockageMonitor` | NW1 (debounce) + NW2 (`HighQualified` holds through glitches, clears on confirmed-clear) |
| REQ-HBA-004 alarm latches until reset | `FB_HopperBlockageMonitor` | NW4 |
| REQ-HBA-005 reset clears alarm | `FB_HopperBlockageMonitor` | NW4 (`NOT FaultReset`) |
| REQ-HBA-006 hopper clearing while latched keeps it | `FB_HopperBlockageMonitor` | NW4 (latch independent of sensor) |
| REQ-HBA-007 power-up / first-scan state | `FB_HopperBlockageMonitor` | **SPLIT & UNDER REVISION (NEW-HBA-04):** partial accumulator resets at power-up (NW3, non-retentive) — kept; latch *survives* per C-124 carve-out (NW4, RETAIN) — contradicts REQ-HBA-007's current text, owner OK pending |
| Q-HBA-04 gate: only while running | `FB_HopperBlockageMonitor` | NW2/NW3 (`PlantRunning` in the accrue condition; pause-resume) |
| Q-HBA-05 stop/inhibit demand | `FB_HopperBlockageMonitor` | NW5 (`HopperBlockStopReq`) |

Every REQ mapped. Q-HBA-03 (alarm sink) is integration-scope by owner deferral: FB exposes
`HopperBlockedAlarm`; annunciation destination decided at integration.

## 8. Cross-instance wiring plan (integration step — OUT of gen-block-new scope)

Per C-127, all in the orchestrating FC (`FC_ControlMain`). Every row edits existing networks →
separate integration step, not this run's deliverable; `converter diff` enforces
`FC_ControlMain`/`OB100`/`DB_*` byte-identical this run.

| Source | → Destination | Status |
|---|---|---|
| `DB_Input.Hopper_Level_High` | `iDB_HopperBlockageMonitor.IO.HopperLevelHigh` | exists → new |
| `DB_Input.Shredder_Run_Fwd_FB OR DB_Input.Shredder_Run_Rev_FB` | `iDB_HopperBlockageMonitor.IO.PlantRunning` | exists → new (OR in FC_ControlMain) |
| `DB_Controls.FaultReset` | `iDB_HopperBlockageMonitor.IO.FaultReset` | exists → new |
| `iDB_HopperBlockageMonitor.IO.HopperBlockedAlarm` | proposed alarm-annunciation bit (Q-HBA-03, deferred) | new → proposed |
| `iDB_HopperBlockageMonitor.IO.HopperBlockStopReq` | proposed stop-integration consumer | new → proposed |
| call `FB_HopperBlockageMonitor` in `FC_ControlMain` | — | integration |

The FB reads only its own `IO` interface — no sibling instance name inside its logic (no C-127
design error).

## 9. Tag status

Grep-verified against `ir/test-project001/` at corpus commit `c8c3dea`.

| Tag / member | Status | Evidence |
|---|---|---|
| `DB_Input.Hopper_Level_High : Bool` | exists | `DB_Input.ir:13` |
| `DB_Input.Shredder_Run_Fwd_FB : Bool` | exists | `DB_Input.ir:10` |
| `DB_Input.Shredder_Run_Rev_FB : Bool` | exists | `DB_Input.ir:11` |
| `DB_Controls.FaultReset : Bool` | exists | `DB_Controls.ir:10` |
| `UDT_HopperBlockageIO`, `FB_HopperBlockageMonitor`, `iDB_HopperBlockageMonitor` + all members | proposed | design-defined; `proposed` until a fresh export shows them |
| alarm-annunciation bit; stop-demand consumer | proposed | integration sinks (Q-HBA-03, deferred) |

No invented physical tags/addresses/DB numbers.

## 10. Open questions

Register questions carried (owner answers now recorded in the register RFI section):
- Q-HBA-01 (60 s), Q-HBA-02 (debounced clear), Q-HBA-04 (only-while-running, feedback signal),
  Q-HBA-05 (two output demands, no shared write), Q-HBA-06 (ID scheme) — **RESOLVED**.
- Q-HBA-03 (alarm sink) — deferred to integration.

Gate-1 override answers recorded: NEW-HBA-01 (pause-resume), NEW-HBA-02 (UDT-IO), NEW-HBA-03 (`T#2S`).

New this revision (**NEW**, for second confirm):
- **NEW-HBA-04 (material)** — revise REQ-HBA-007: the alarm latch **survives** the power cycle
  (RETAIN, C-124 carve-out, `DischargeConveyorTimeoutFault` precedent), cleared only by `FaultReset`
  — correcting v1's C-128 misreading. Changes a signed register REQ; owner OK needed.
- **NEW-HBA-05 (material)** — pause-resume timer: TON + non-retentive accumulator (C-406-compliant,
  recommended) vs a single TONR (simpler, but a C-406 error-rule exception needing documented OK;
  corpus `HrTotaliserTimer` precedent).
- **NEW-HBA-06 (minor)** — transient-state power-up reset: rely on non-retentivity (in-scope,
  recommended) vs belt-and-suspenders OB100 clear (expanded scope, C-124 letter).
- **NEW-HBA-07 (minor)** — C-115 vocabulary on a supervisory monitor: unwired enable/ready/running
  stubs vs a documented monitor exemption (recommended).

---

## Gate-1 revision — second-confirm checklist

Gate 1 was signed at v1. These items arose from reconciling the v1→v2 overrides and need a focused
owner confirm before coding (Stage 3):

- [x] **NEW-HBA-04** — REQ-HBA-007 revised: alarm latch is RETAIN and survives power cycle (C-124
      carve-out), not cleared at power-up. *(material — changes a signed REQ)* — **owner-confirmed
      2026-07-19; register REQ-HBA-007 revised.**
- [x] **NEW-HBA-05** — pause-resume mechanism: **(i) TON + Time arithmetic, no TONR** — owner chose
      (i). *(material)* — Build note: realized as a **countdown-remaining-budget** formulation
      (`RemainingTime := BlockedTimeThreshold`, banked down by each segment's elapsed via `T_SUB` on
      the pause edge; `PersistenceTimer.Q` trips when a segment exhausts the remaining budget),
      rather than the literal "up-accumulator + `>=` comparison" wording. Identical behaviour, still
      TON-not-TONR (C-406). See §6 "Build reconciliation" for why the up-accumulator+compare wording
      was not built verbatim (converter Gap E) — surfaced during Stage-3 static validation.
- [x] **NEW-HBA-06** — transient reset via non-retentivity *(in-scope)* — owner chose in-scope; no
      OB100 edit. *(minor)*
- [x] **NEW-HBA-07** — C-115 vocabulary: documented supervisory-monitor exemption *(recommended)* —
      owner chose the exemption; no enable/ready/running stubs. Input gate is `PlantRunning`. *(minor)*

No item forces an out-of-scope build step: `UDT_HopperBlockageIO` + `FB_HopperBlockageMonitor` +
`iDB_HopperBlockageMonitor` are all additive (in-scope); no OB100 edit, no shared write.

## Gate-1 sign-off (docs/11)

**Signed off by the project owner via orchestrated Gate-1 review, 2026-07-19.** Decisions recorded:
build it; 100% freeform go-ahead (workflow step 3); inhibit delivered as an OUTPUT DEMAND
(`HopperBlockStopReq`) with **no shared write** (stop wiring + alarm annunciation are a separate
integration step, `converter diff`-enforced); NEW-HBA-01 pause-and-resume; NEW-HBA-02 corpus UDT-IO;
NEW-HBA-03 `ClearDebounceTime := T#2S`; NEW-HBA-04 alarm latch RETAIN survives power cycle (C-124);
NEW-HBA-05 TON + Time arithmetic (no TONR); NEW-HBA-06 non-retentive transient state (no OB100);
NEW-HBA-07 supervisory-monitor C-115 exemption.

Stage 3 (`gen-block-new` / build) proceeds on this sign-off.

- [x] Owner sign-off: orchestrated Gate-1 review, 2026-07-19.
