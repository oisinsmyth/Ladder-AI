# test-project001 — Hopper-Blockage Alarm — Requirements Register

Request-scoped behavioural requirements register for the owner's plain-language request to add an FB
that raises a hopper-blockage alarm when the hopper stays continuously high too long (S6 generation
request #1 of 10, owner-pivoted from the abandoned discharge-conveyor supervisor — that request was
a duplicate of existing `FB_ShredderSequencer` logic and was dropped). Produced by
`docs/15-generation-pipeline.md`'s `gen-spec-analysis` stage, **performed manually** (the skill does
not exist yet — docs/15 build-order). Input artifact for the `review-functional` check stage of this
request's build; the REQ IDs below are stable forever.

Scope is **this one FB only**. It does not restate the whole-project register
(`gen/test-project001/requirements.md`, 69 REQs). Related existing REQs there: REQ-014 (hopper high
stops infeed, shredder keeps running) and REQ-034 (hopper high triggers one pusher auto-cycle) —
both are *actions triggered by* hopper-high, none is a *persistence* alarm. See the reuse-first
result below.

## Provenance

- **Produced:** 2026-07-19, `manual:gen-spec-analysis`. Stage 1 (Analyse) of a `gen-block-new` run.
- **Primary source:** the owner's verbatim plain-language request (S6 request #1, pivoted). No other
  source document was consulted for requirement text.
- **Request (verbatim):** "Add an FB that raises a hopper-blockage alarm when `Hopper_Level_High`
  stays continuously high longer than a threshold time (the shredder/pusher isn't clearing it);
  clear the alarm on `FaultReset`."
- **Reuse-first / non-duplication result (grep-verified, `ir/test-project001/`):** no existing block
  monitors `Hopper_Level_High` for *persistence* or raises a hopper-blockage / throughput alarm.
  `Hopper_Level_High` (`DB_Input`) is consumed in exactly two places, neither a persistence timer or
  alarm: (1) `FB_ShredderSequencer.ir:182` times the hopper being **not** high
  (`InfeedRestartTimer`, `NOT IO.HopperLevelHigh` at Step 50 — the infeed-restart delay after the
  hopper clears, the opposite condition); (2) `FB_PusherControl.ir:108` uses hopper-high as an
  instantaneous auto-mode cycle trigger (`CycleRequest`). The two existing latched "Blocked" faults
  are unrelated to hopper level: `FB_PusherControl` `IO.Blocked` counts high-pressure trips
  (`FB_PusherControl.ir:152`), and `FB_ShredderSequencer` `IO.ShredderBlockedFault` counts reversals
  in a window (`FB_ShredderSequencer.ir:151`). The requested "hopper stays high too long" alarm is
  genuinely absent — proceeding is not a duplication.
- **Tag-status verification corpus:** `ir/test-project001/` as of the current working tree
  (branch `master`). Every `exists` mark was grep-verified at write time (hard rule 3).
- **WHAT, never HOW:** records what was asked, not how it will be built. FB interface, network
  layout, timer type, latch structure, and the concrete threshold value are Stage 2
  (`gen-architecture`). Where the request is silent, the gap is an RFI, never a silently-invented
  answer.

## Format

Follows the whole-project register's contract (its "Format" section is the defining instance).
Same one deviation flagged in the prior scoped run, re-flagged at RFI-Q-HBA-06:

- **IDs:** `REQ-HBA-nnn` (HBA = hopper-blockage-alarm), assigned in source order, **stable forever**.
  The scoped `-HBA-` infix is used deliberately instead of the bare `REQ-nnn` shape so these IDs
  cannot collide with the whole-project register's global `REQ-001…069` in the shared S9 sim-trace
  namespace. Format decision, not a documented convention yet — see RFI-Q-HBA-06.
- **Classes** (one per REQ): `control`, `alarm`, `HMI`, `mode`, `timing`, `out-of-scope` — same
  meanings as the whole-project register.
- **Tag status:** every tag/DB member named is marked `exists` (grep-verified) or `proposed`
  (a named gap the engineer creates — hard rule 3). Nothing may be coded against a `proposed` tag.
- **Open questions:** `Q-HBA-nn`, in their own section. No stage silently resolves one.

## Tag grounding

Both tags the request names are `exists` in `ir/test-project001/`.

| Tag | Home DB | Type | Status | Evidence |
|---|---|---|---|---|
| `Hopper_Level_High` (blockage condition, read-only to this FB) | `DB_Input` | `Bool` | exists | `DB_Input.ir:13` |
| `FaultReset` (operator reset) | `DB_Controls` | `Bool` | exists | `DB_Controls.ir:10` |

**Persistence threshold — design note, not a proposed shared tag.** There is **no** hopper-timeout
member in `DB_Settings` (grep-verified: `DB_Settings.ir` has no `Hopper*` member). Per this request's
scope, the threshold time is the **FB's own Constant/Static tunable**, not a new shared-DB setting
(adding a `DB_Settings` member is outside `gen-block-new` scope — that is an engineer/DB-edit action).
Recorded here as a design constraint for Stage 2; it is **not** a proposed shared tag. The actual
numeric value is unspecified by the request — see RFI-Q-HBA-01.

**Alarm sink — proposed.** The hopper-blockage alarm's annunciation bit (where it lands, e.g. a new
`DB_Alarms` bit following the existing `ShredderAlarm0.%Xn` pattern) is a Stage-2 / alarm-design
decision and is **proposed** until then — see RFI-Q-HBA-03.

## Requirements

### Persistence-alarm path

### REQ-HBA-001 — Continuous-high blockage alarm
- **Text:** When `Hopper_Level_High` remains continuously true for longer than the persistence
  threshold time, a hopper-blockage alarm is raised.
- **Class:** alarm
- **Source:** Request — "raises a hopper-blockage alarm when Hopper_Level_High stays continuously
  high longer than a threshold time".
- **Notes:** The threshold is the FB's own tunable (see tag grounding); value is RFI-Q-HBA-01.
  Whether the alarm also stops/inhibits any equipment is not stated — RFI-Q-HBA-05.

### REQ-HBA-002 — No alarm below threshold
- **Text:** If `Hopper_Level_High` clears (goes false) before the persistence threshold elapses, no
  hopper-blockage alarm is raised for that high episode.
- **Class:** alarm
- **Source:** Request (the testable inverse of REQ-HBA-001's continuous-high condition).
- **Notes:** Boundary requirement for the functional review; pairs with REQ-HBA-001.

### REQ-HBA-003 — Momentary clear re-arms the window
- **Text:** The alarm condition requires `Hopper_Level_High` to be *continuously* high: any
  transition of `Hopper_Level_High` to false before the threshold elapses resets the persistence
  timing, so the full threshold must accumulate again from the next rising edge. A momentary clear
  does not leave a partially-elapsed window that would trip on a later brief high.
- **Class:** alarm
- **Source:** Request — "stays continuously high"; coordinator instruction that a momentary clear
  re-arms the window.
- **Notes:** Exact debounce/minimum-clear semantics (does *any* single-scan false reset it, or is a
  qualified/debounced clear intended, to reject sensor chatter) — RFI-Q-HBA-02.

### Alarm latch and reset

### REQ-HBA-004 — Alarm latches until reset
- **Text:** Once raised, the hopper-blockage alarm remains asserted regardless of any subsequent
  change in `Hopper_Level_High`, until it is reset.
- **Class:** alarm
- **Source:** Request — "raises a hopper-blockage alarm … clear the alarm on FaultReset" (implies a
  latch that persists until reset, not a live level-follow of the sensor).
- **Notes:** Latch is level-persistent, not a momentary pulse.

### REQ-HBA-005 — Reset clears the alarm
- **Text:** Asserting `FaultReset` clears the latched hopper-blockage alarm.
- **Class:** alarm
- **Source:** Request — "clear the alarm on FaultReset".
- **Notes:** `DB_Controls.FaultReset` is the shared operator reset already routed to the
  sequencer/pusher/motor faults in the corpus (`FC_ControlMain`); this FB uses the same signal.

### REQ-HBA-006 — Hopper clearing while alarm latched does not clear it
- **Text:** `Hopper_Level_High` returning to false while the hopper-blockage alarm is latched does
  not by itself clear the alarm; it persists until `FaultReset`.
- **Class:** alarm
- **Source:** Request (derived edge case — separates the latch's clear condition from the sensor).
- **Notes:** Makes explicit that reset, not the level dropping, is the only clear path (with
  REQ-HBA-004). The operator acknowledges the blockage cleared, rather than the alarm self-clearing.

### Power-up / retentive state

### REQ-HBA-007 — Power-up / first-scan state
- **Text:** At PLC power-up / first scan the **transient** persistence state starts fresh — no
  partially-elapsed window, held-elapsed value, or in-progress timer survives the power cycle. The
  **latched hopper-blockage alarm does survive** the power cycle (it is a genuine fault requiring
  operator acknowledgement) and is cleared only by `FaultReset`; the associated stop/inhibit demand
  correspondingly persists until acknowledged.
- **Class:** alarm
- **Source:** Request (derived edge case). **Revised at Gate-1 (NEW-HBA-04, owner 2026-07-19)** to
  align with doc 06 **C-124**'s explicit carve-out and **C-128**'s actual scope. C-128 forbids
  automatic *motion* restart, not fault-latch persistence; C-124 deliberately **excludes** a genuine
  `FaultReset`-cleared fault latch from the OB100 startup clear ("a fault needing human
  acknowledgement still needs it after a power cycle"). The corpus precedent is
  `iDB_ShredderSequencer.IO.DischargeConveyorTimeoutFault` — a `RETAIN` fault latch, explicitly
  excluded from OB100 clearing, that survives the power cycle. A surviving blockage alarm is
  fail-safe: it keeps the stop demand asserted until a human clears the blockage and acknowledges.
- **Notes:** Was previously (pre-Gate-1) worded "no latched alarm survives the power cycle," derived
  from a C-128 misreading; corrected here. The alarm/stop-demand outputs are RETAIN; the timers,
  the debounce, the cumulative budget, and the qualifier latch are non-retentive (reset at power-up
  by non-retentivity — NEW-HBA-06, no OB100 edit).

## Open questions (RFIs)

Never silently resolved; resolution is a recorded owner answer noted at the question.

- **Q-HBA-01 — Persistence threshold value. RESOLVED (owner, 2026-07-19): 60 seconds.** Stored as
  the FB's own `TIME` constant/tunable (`T#60S`), commissioning-changeable — **not** a shared
  `DB_Settings` member (out of `gen-block-new` scope). Drives REQ-HBA-001's persistence window.
- **Q-HBA-02 — Re-arm / debounce semantics. RESOLVED (owner, 2026-07-19): debounced clear.** A
  momentary/brief clear of `Hopper_Level_High` must NOT reset the persistence window — the hopper
  must read clear continuously for a short filter time before the window re-arms. Implemented as a
  short off-delay/debounce on the clear, its filter time a second FB-own `TIME` tunable. Refines
  REQ-HBA-003 (a one-scan glitch to false no longer re-arms).
- **Q-HBA-03 — Alarm sink / annunciation bit. DEFERRED to integration (owner, 2026-07-19).** Out of
  this run's scope: the FB exposes the alarm on its own output param only; where it annunciates (a
  free `ShredderAlarm0` bit or a new `DB_Alarms` bit) is decided at the separate integration step,
  not `gen-block-new`.
- **Q-HBA-04 — Gating: run-state conditioning. RESOLVED (owner, 2026-07-19): only while running.**
  The persistence window accumulates only while the shredder/plant is actually running (a full
  hopper at standstill does not alarm). Run-state signal chosen: confirmed-running **feedback**,
  either direction — `DB_Input.Shredder_Run_Fwd_FB OR DB_Input.Shredder_Run_Rev_FB` (both `exists`,
  `DB_Input.ir:10–11`). Feedback (not the `Run_Shredder_*` commands) is the honest "actually
  running" signal, consistent with the whole-project register's REQ-069 owner ruling that the
  in-cycle lamp is taken from field running feedbacks, not PLC run commands.
- **Q-HBA-05 — Alarm action. RESOLVED (owner, 2026-07-19): also inhibit/stop, as an FB OUTPUT
  DEMAND.** The FB emits two outputs on its own interface — a `HopperBlockedAlarm` (Bool) and an
  inhibit/stop **demand** bit — and writes **no** shared DB/output tag. The actual stop wiring and
  the alarm annunciation happen at a separate integration step (editing existing networks is out of
  `gen-block-new` scope; `converter diff` enforces that line). See the architecture manifest's
  gate-1 decision line.
- **Q-HBA-06 — REQ ID scheme. RESOLVED (owner, 2026-07-19): accepted.** The scoped `REQ-HBA-nnn`
  scheme stands for request-scoped registers.

### Gate-1 design questions (from `architecture.md`)

- **NEW-HBA-01 — running-loss semantics. RESOLVED (owner Gate-1, 2026-07-19): pause-and-resume.**
  Losing `PlantRunning` mid-window FREEZES the elapsed persistence time and resumes from there; the
  alarm needs cumulative (not necessarily contiguous) high-and-running time to reach threshold. (Not
  reset-on-stop.) Refines REQ-HBA-001's "continuously high" to "cumulatively high-and-running".
- **NEW-HBA-02 — interface style. RESOLVED (owner Gate-1, 2026-07-19): corpus UDT-IO.** The FB
  carries one `IO : "UDT_HopperBlockageIO" RETAIN SETPOINT` per the corpus handshake convention
  (not plain INPUT/OUTPUT sections). New UDT `UDT_HopperBlockageIO` created (additive, in-scope).
- **NEW-HBA-03 — debounce filter value. RESOLVED (owner Gate-1, 2026-07-19): `T#2S` confirmed.**
  `ClearDebounceTime` start value `T#2S`, commissioning-tunable.
- **NEW-HBA-04 — power-up state of the alarm latch vs REQ-HBA-007. OPEN (second-confirm at design
  revision).** Reconciling NEW-HBA-02 (RETAIN UDT-IO) against the rule text corrected an error in
  the v1 design's reasoning: **C-128 governs no automatic *motion* restart, not fault-latch
  clearing**, and **C-124 deliberately excludes genuine `FaultReset`-cleared fault latches from the
  OB100 startup clear** ("a fault needing human acknowledgement still needs it after a power cycle").
  The corpus's own `iDB_ShredderSequencer.IO.DischargeConveyorTimeoutFault` (closest sibling) is
  `RETAIN` and survives the power cycle. So `HopperBlockedAlarm` should be **RETAIN and survive**,
  cleared only by `FaultReset` — which contradicts REQ-HBA-007's current wording ("no latched alarm
  survives the power cycle"). **Proposed revision (owner OK needed):** REQ-HBA-007 keeps its
  partial-window-resets-at-power-up clause but the *latched alarm survives* per the C-124 carve-out
  (fail-safe: a live blockage stays annunciated + stop-demanded until acknowledged). Not silently
  applied — flagged as a change to a signed REQ.
- **NEW-HBA-05 — pause-resume timer mechanism vs C-406. OPEN (second-confirm).** C-406's rule text
  is a blanket ban on TONR (error). Recommended: TON + a non-retentive accumulator (C-406-compliant).
  Alternative: a single TONR (simpler; a C-406 error-rule exception needing documented owner OK;
  the corpus `FB_MotorFwdRevSystem.HrTotaliserTimer` TONR is a pre-existing deviation). Owner's call.
