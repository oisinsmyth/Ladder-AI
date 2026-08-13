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
- **Agent rulings:** `AR-HBA-nn`, in the "Pipeline rulings" section. These are **rulings made by the
  `lad-coder` pipeline agent, NOT by the owner** — they exist so the register stops being ambiguous
  where an ambiguity was blocking work, and every one of them is **overturnable by the owner editing
  a single line**. Every clause an `AR-HBA-nn` changed says so at the point of change. Do not read an
  `AR-HBA-nn` as an owner answer, and do not promote one to `Q-HBA`/`NEW-HBA` status without an
  actual owner reply.
- **Numeric bounds are not part of assertion identity.** Owner-fixed but commissioning-changeable
  values (the persistence threshold, the clear-debounce filter time) are carried **beside** a
  requirement, never welded into the sentence a downstream assertion hashes — see AR-HBA-03.
- **Signal names are not part of assertion identity either — preserve this deliberately.** A signal
  name belongs in an assertion's `response_signal:` field, never inside the hashed sentence. Measured
  on the AR-HBA-01 round: **naming the previously-unnamed output cost ZERO re-hashes**, because no
  assertion text contained a signal name to begin with. That is a structural property worth keeping —
  it means an interface can be named, renamed or corrected without dangling a single citation. An
  assertion sentence that embeds a signal name gives that property away for the whole register.

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
- **Notes:** The threshold is the FB's own tunable (see tag grounding); value is RESOLVED at
  Q-HBA-01 and carried in the "Commissioning-changeable bounds" table (AR-HBA-03), not in this
  sentence. The alarm also raises an inhibit/stop demand — RESOLVED at Q-HBA-05; the two outputs are
  `HopperBlockedAlarm` and `HopperBlockedInhibit` (the latter named by AR-HBA-01). "Continuously
  true" is refined by NEW-HBA-01 to *cumulatively* high-and-running.

### REQ-HBA-002 — No alarm below threshold
- **Text:** If `Hopper_Level_High` undergoes a **debounced clear** — it goes false and stays
  continuously false for at least the clear-debounce filter time — before the accumulated persistence
  time reaches the threshold, no hopper-blockage alarm is raised for that high episode. A false
  reading shorter than the filter time is **not** a clear (Q-HBA-02) and does not, by itself, prevent
  the alarm.
- **Class:** alarm
- **Source:** Request (the testable inverse of REQ-HBA-001's continuous-high condition).
- **Notes:** Boundary requirement for the functional review; pairs with REQ-HBA-001. Threshold and
  filter values: see the "Commissioning-changeable bounds" table (AR-HBA-03).
- **Amended by AR-HBA-04 (agent ruling, not owner).** The original wording — "clears (goes false)" —
  literally contradicted Q-HBA-02, which says a brief false is not a clear. Q-HBA-02 is the later,
  owner-resolved statement and governs; this clause is reworded to match it rather than left as a
  contradiction for a downstream reader to resolve silently.

### REQ-HBA-003 — Momentary clear re-arms the window
- **Text:** A **debounced clear** of `Hopper_Level_High` (false continuously for at least the
  clear-debounce filter time) **discards** the accumulated persistence time, so the full threshold
  must accumulate again from scratch after the hopper next goes high. A momentary clear shorter than
  the filter time does not discard it. A debounced clear does not leave a partially-elapsed window
  that would trip on a later brief high.
  **While the filter time is still running** — the hopper reads false but has not yet been false long
  enough to be a debounced clear — **nothing is discarded**: the accumulated time is held, and if the
  plant is also stopped it stays **frozen** (AR-HBA-09). The clear-debounce filter itself runs
  **irrespective of `PlantRunning`**, which is what allows a debounced clear to be established while
  the plant is stopped at all.
- **Class:** alarm
- **Source:** Request — "stays continuously high"; coordinator instruction that a momentary clear
  re-arms the window; refined by Q-HBA-02 (debounced clear) and NEW-HBA-01 (cumulative, not
  contiguous, high-and-running time).
- **Notes:** Debounce/minimum-clear semantics are RESOLVED at Q-HBA-02 (debounced clear, filter time
  per AR-HBA-03's table). "Continuously high" was already superseded by NEW-HBA-01's cumulative
  reading; what a debounced clear discards is that **cumulative** accumulator.
- **Reconciled with NEW-HBA-01 by AR-HBA-06 (agent ruling, not owner).** A debounced clear discards
  the accumulator **whether or not the plant is running** — the discard beats the freeze. See
  AR-HBA-06 for the reasoning.
- **Sub-filter-time gap closed by AR-HBA-09 (agent ruling, not owner).** AR-HBA-06's narrowed freeze
  scope left one case uncovered: plant stopped, hopper reading false, filter time not yet elapsed.
  It **freezes**. See AR-HBA-09.

### Alarm latch and reset

### REQ-HBA-004 — Alarm latches until reset
- **Text:** Once raised, the hopper-blockage alarm remains asserted regardless of any subsequent
  change in `Hopper_Level_High`, until it is reset.
- **Class:** alarm
- **Source:** Request — "raises a hopper-blockage alarm … clear the alarm on FaultReset" (implies a
  latch that persists until reset, not a live level-follow of the sensor).
- **Notes:** Latch is level-persistent, not a momentary pulse.

### REQ-HBA-005 — Reset clears the alarm
- **Text:** A **rising edge** of `FaultReset` clears the latched hopper-blockage alarm and, with it,
  the inhibit/stop demand. The clear is **not** a suppression: the alarm condition is re-evaluated
  immediately afterwards, and **the condition it re-evaluates is the accumulated persistence time
  alone** — at or past the threshold means **the alarm re-raises**, *whether or not the plant is
  running* (AR-HBA-07). A reset therefore cannot hold a live fault off, and holding `FaultReset` on
  continuously cannot pin the alarm off.
  **What is observable after the reset, by case (AR-HBA-08):**
  - **The condition still holds** (accumulated time still at or past the threshold) — the alarm is
    **true** when next observed. Whether it went momentarily false in between is **DON'T-CARE**: the
    re-raise is immediate, so any drop is a sub-scan transient of no operational consequence, and
    "never went low" is **not** a failure.
  - **The condition has cleared** (a debounced clear has discarded the accumulated time, so it is
    below the threshold) — the alarm goes **false and stays false**, and that false is **REQUIRED to
    be observable**. Without it "the reset worked" and "the reset did nothing" are indistinguishable,
    which is the whole point of this clause.
- **Class:** alarm
- **Source:** Request — "clear the alarm on FaultReset".
- **Notes:** `DB_Controls.FaultReset` is the shared operator reset already routed to the
  sequencer/pusher/motor faults in the corpus (`FC_ControlMain`); this FB uses the same signal.
  `FaultReset` does **not** touch the accumulated persistence time (AR-HBA-05) — the accumulator is
  governed only by hopper-level evidence and run state (REQ-HBA-003, NEW-HBA-01, AR-HBA-06).
- **Determined by AR-HBA-05 (agent ruling, not owner)** on three points the request left silent:
  - **Edge, not level.** The reset acts on the rising edge of `FaultReset`. A held-on reset does not
    keep the alarm cleared.
  - **Raise-dominant.** If a reset and a satisfied raise condition coincide in the same evaluation,
    the raise wins and the alarm ends that evaluation asserted.
  - **The accumulated persistence time is untouched by reset** — it is neither zeroed nor frozen.
    Consequence, stated so it is testable: resetting while the hopper is still high and the plant
    still running produces an alarm that re-raises essentially at once, **not** one that re-arms
    from zero and stays quiet for another threshold's worth of time.
- **Re-raise condition fixed by AR-HBA-07 (agent ruling, not owner).** The re-raise keys on the
  **accumulator alone**, never on `PlantRunning`. So the sequence *stop the plant → reset → hopper
  still high, accumulator frozen past threshold* **re-raises**. See AR-HBA-07.
- **Observability of the de-assert fixed by AR-HBA-08 (agent ruling, not owner).** The earlier wording
  "drops (if at all)" left a vector unable to tell whether "never went low" passes or fails; the Text
  above now splits it by case. See AR-HBA-08.

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
  operator acknowledgement) and is cleared only by `FaultReset`; `HopperBlockedInhibit`
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
- **Bookkeeping closed by AR-HBA-02 (agent ruling, not owner).** NEW-HBA-04 was listed OPEN while the
  revision it proposed was already present in this clause's Text. That was a contradiction about
  *whether a signed REQ had changed*, not about behaviour. NEW-HBA-04 is now RESOLVED; this clause's
  Text is unchanged by that closure.

## Open questions (RFIs)

Never silently resolved; resolution is a recorded owner answer noted at the question.

- **Q-HBA-01 — Persistence threshold value. RESOLVED (owner, 2026-07-19): 60 seconds.** Stored as
  the FB's own `TIME` constant/tunable (`T#60S`), commissioning-changeable — **not** a shared
  `DB_Settings` member (out of `gen-block-new` scope). Drives REQ-HBA-001's persistence window.
  Carried as a **bound**, not as requirement text — AR-HBA-03.
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
  inhibit/stop **demand** bit, **named `HopperBlockedInhibit` (Bool) by AR-HBA-01 — an agent ruling,
  not the owner's** — and writes **no** shared DB/output tag. The actual stop wiring and
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
  **Scope narrowed by AR-HBA-06 (agent ruling, not owner):** the freeze applies only while the hopper
  is still reading high. A **debounced clear** of `Hopper_Level_High` discards the accumulator even
  when the plant is stopped — the discard beats the freeze. See AR-HBA-06.
  **Scope completed by AR-HBA-09 (agent ruling, not owner):** the freeze also applies while the hopper
  reads false but the clear-debounce filter time has **not** yet elapsed — no clear has been
  established, so there is nothing to discard. See AR-HBA-09.
- **NEW-HBA-02 — interface style. RESOLVED (owner Gate-1, 2026-07-19): corpus UDT-IO.** The FB
  carries one `IO : "UDT_HopperBlockageIO" RETAIN SETPOINT` per the corpus handshake convention
  (not plain INPUT/OUTPUT sections). New UDT `UDT_HopperBlockageIO` created (additive, in-scope).
- **NEW-HBA-03 — debounce filter value. RESOLVED (owner Gate-1, 2026-07-19): `T#2S` confirmed.**
  `ClearDebounceTime` start value `T#2S`, commissioning-tunable. Carried as a **bound**, not as
  requirement text — AR-HBA-03.
- **NEW-HBA-04 — power-up state of the alarm latch vs REQ-HBA-007. RESOLVED — closed as already
  applied (AR-HBA-02, agent ruling 2026-08-13, not owner).** The proposed revision below **is already
  present verbatim in REQ-HBA-007's Text**, which credits it to the owner's 2026-07-19 Gate-1 pass.
  Leaving this item OPEN made the register contradict itself about whether a signed REQ had changed,
  which is a bookkeeping fault and not a behavioural question — so it is closed on the evidence of
  the clause text, with no change to that text. If the owner did **not** in fact sign that revision,
  overturning this is a one-line edit here and a revert of REQ-HBA-007's Text. Reconciling NEW-HBA-02 (RETAIN UDT-IO) against the rule text corrected an error in
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

## Pipeline rulings (agent, NOT owner)

Made 2026-08-13 by the `lad-coder` pipeline agent, to close the ambiguities raised by the third-party
assertion enumeration of this register
(`gen/test-project001/hopper-blockage-alarm/assertion-enumeration.yaml`, AMB-01…AMB-08). The register,
not the dispatch message that requested them, is the authority for each.

**None of these is an owner ruling.** Each is overturnable by the owner editing the single line marked
`RULING:` below (and reverting the clause edits listed under `CHANGED:`). Where a ruling was made
without consulting some source, that abstention is recorded — it is deliberate, not an oversight.

Two of the eight ambiguities are **NOT ruled here and remain open**: **AMB-05** (a contradiction inside
the method document `docs/notes/assertion-enumeration.md` §1 vs §2.2 — a dispute against the method,
not against this register, so it is not this register's to close) and the response-signal /
`Expectations` set-difference gap AMB-03 also describes, which AR-HBA-01 fixes only for this register.

### AR-HBA-01 — the inhibit/stop demand output is named `HopperBlockedInhibit` *(closes AMB-03)*

**RULING:** the second FB output required by Q-HBA-05 is named **`HopperBlockedInhibit`** (Bool).

**Reasoning.** Q-HBA-05 resolved that the FB emits two outputs but named only one, so three assertions
could carry no `ResponseSignal` and the mechanical check "the cited assertion's response signal appears
in the vector's `Expectations`" could not run for them. That is an inflation route open by omission.
The name is derived from Q-HBA-05's own words ("inhibit/stop demand") and from the sibling output it
pairs with, `HopperBlockedAlarm`.

**Deliberate abstention — read this before "correcting" the name.** The implementation
(`ir/test-project001/FB_HopperBlockageMonitor.ir`, `UDT_HopperBlockageIO.ir`) was **not read** when this
name was chosen. The register is the specification and the implementation is the thing under test; if
the block's actual output is named something else, **that is a finding about the block, not a defect in
this register.** Copying the implementation's name into the spec would make the spec and the block agree
by construction — the correlated check this pipeline exists to prevent. Renaming the register to match
the block is therefore **not** an acceptable resolution of such a mismatch; either the block is renamed,
or the owner overturns this ruling on grounds other than "the block already says X".

**CHANGED:** Q-HBA-05, REQ-HBA-001 Notes, REQ-HBA-007 Text.

### AR-HBA-02 — NEW-HBA-04 is closed as already applied *(closes AMB-04)*

**RULING:** NEW-HBA-04 is **RESOLVED**, not OPEN. No requirement text changes as a result.

**Reasoning.** REQ-HBA-007's Text already contains the revision NEW-HBA-04 proposed, and attributes it
to the owner's Gate-1 pass of 2026-07-19. A register that simultaneously records a revision as applied
and as awaiting approval contradicts itself about whether a signed REQ changed — and every citation to
REQ-HBA-007 would go STALE if the item were later reopened and the clause reworded. Closed on the
evidence of the clause's own text. This is a bookkeeping correction with no behavioural content.

**CHANGED:** NEW-HBA-04 (status line + reason), REQ-HBA-007 Notes (closure note).

### AR-HBA-03 — commissioning-changeable bounds stay out of assertion identity *(closes AMB-06)*

**RULING:** the enumerator's treatment is **CONFIRMED**. The persistence threshold and the
clear-debounce filter time are carried **beside** the requirements as bounds, never welded into the
sentence a downstream assertion hashes. Assertion identity does not change when either value is
retuned.

**Reasoning.** Both values are owner-fixed *and* explicitly commissioning-changeable (Q-HBA-01,
NEW-HBA-03). Baking a value into hashed assertion text means a commissioning retune re-hashes every
assertion that mentions it and dangles every citation to them — a documentation avalanche caused by an
event the register already anticipates as normal. The requirement is "longer than the persistence
threshold"; the number is a parameter of the test, not of the claim. A test vector still exercises a
concrete value — it reads it from this table and says which it used.

| Bound | Value | Source | Where it lives at runtime |
|---|---|---|---|
| Persistence threshold | `T#60S` | Q-HBA-01 (owner) | FB's own `TIME` tunable, not `DB_Settings` |
| Clear-debounce filter time | `T#2S` | NEW-HBA-03 (owner) | FB's own `ClearDebounceTime` `TIME` tunable |

Changing a value in this table is **not** a requirement change and does not re-hash anything. Changing
which *quantities* exist is.

**CHANGED:** Format section (new bullet), REQ-HBA-001/002 Notes, Q-HBA-01, NEW-HBA-03.

### AR-HBA-04 — REQ-HBA-002 reworded to the debounced reading *(closes AMB-08)*

**RULING:** Q-HBA-02's debounced clear **governs**; REQ-HBA-002's text is amended so it no longer
literally contradicts it.

**Reasoning.** REQ-HBA-002 said "clears (goes false)"; Q-HBA-02 says a brief false is not a clear.
Q-HBA-02 is the later and owner-resolved statement, and it was written specifically to refine this
family of clauses. A contradiction left standing is resolved silently and differently by each
downstream reader — which is the failure this pipeline is built to catch, so it is fixed in the text
rather than annotated.

**CHANGED:** REQ-HBA-002 Text + Notes.

### AR-HBA-05 — reset is edge-triggered, raise-dominant, and does not touch the accumulator *(closes AMB-01 and AMB-02)*

**RULING:** on a rising edge of `FaultReset` the alarm latch is cleared and then **re-evaluated**; if
the blockage condition still holds the alarm **re-raises**. `FaultReset` is **edge-triggered**. A
coincident reset and raise resolve **raise-dominant**. The **accumulated persistence time is untouched
by reset** — neither zeroed nor frozen.

**Reasoning.** AMB-01 and AMB-02 are one question, because the edge/level and dominance choices are
exactly what decides whether a reset can suppress a live fault.

- *Re-raise, not re-arm-from-zero.* A reset must not be able to suppress a fault that is still true.
  The re-arm-from-zero reading gives an operator a guaranteed quiet threshold's-worth of time on a
  hopper that is still blocked, which inverts the alarm's purpose. Re-raise is the conventional and
  the safe direction.
- *Edge, not level.* Under a level-sensitive reset a stuck-on or wired-on `FaultReset` pins the alarm
  off permanently and silently. An edge cannot do that.
- *Raise-dominant.* The same argument at one-scan resolution: set-dominance is what makes "a reset
  cannot suppress a live fault" true in the coincident case too.
- *Accumulator untouched — and this is the point of consistency with AR-HBA-06.* The accumulator's
  discipline is **evidence about the hopper**: it accrues on high-and-running, freezes when running is
  lost, and is discarded on positive evidence that the hopper cleared. `FaultReset` is an operator
  acknowledgement of an alarm; it is **not** evidence about the hopper. So it must not be allowed to
  alter the accumulator. Zeroing on reset would be exactly the re-arm-from-zero behaviour rejected
  above, arriving by a side door.

**Testable consequence, stated so a vector can be written against it:** with the hopper high, the plant
running, and the alarm latched, a pulse on `FaultReset` yields an alarm that drops (if at all) for
essentially one evaluation and is asserted again — **not** an alarm that stays clear for another
threshold period.

**CHANGED:** REQ-HBA-005 Text + Notes.

### AR-HBA-06 — a debounced clear beats the running-loss freeze *(closes AMB-07)*

**RULING:** when the plant is stopped **and** `Hopper_Level_High` then undergoes a debounced clear, the
accumulated persistence time is **DISCARDED**. The discard wins over the freeze.

**Reasoning.** NEW-HBA-01 froze the accumulator on loss of running; REQ-HBA-003 and Q-HBA-02 discard it
on a debounced clear; the register never said which applies when both do. The two rules are not
actually peers, and that is what settles it:

- The **freeze** exists because **blockage cannot be assessed while stopped**. It is a rule about
  *missing evidence* — it stops the accumulator decaying, or accruing, on a measurement that means
  nothing while nothing is moving. It is an argument from *absence*.
- The **discard** exists because **the hopper physically cleared**. Hopper level is a real observable
  fact and it does not stop being one when the plant stops; a debounced clear is a genuine
  measurement, deliberately filtered against chatter. It is an argument from *presence*.

**Positive evidence that the blockage is gone beats the absence of evidence that it is still there.**
Ruling the other way keeps a stale, nearly-elapsed accumulator alive across a stop during which the
hopper visibly emptied, so the next brief high alarms immediately on a hopper that is not blocked —
a nuisance trip built on a measurement the freeze itself declared untrustworthy. The freeze's own
justification therefore cannot support retaining the accumulator once the hopper has spoken.

**Scope of the freeze after this ruling:** the freeze applies while the plant is stopped **and the
hopper is still reading high**. It is not a blanket suspension of the whole accumulator's state
machine.

**Worked case, the one the enumerator raised:** hopper high, plant running, 40 s accumulated of a 60 s
threshold. Plant stops (accumulator freezes at 40 s). Hopper then reads false continuously for ≥ 2 s.
→ **Accumulator discarded, back to 0 s.** On the next start with the hopper high, the full 60 s must
accumulate again. Under the rejected reading it would have alarmed after 20 s.

**CHANGED:** REQ-HBA-003 Text/Source/Notes, NEW-HBA-01 (scope narrowing).

---

## Second round (2026-08-13) — rulings AR-HBA-07…09

The enumerator re-decomposed against the amended text above and returned **19 assertions**, not the 14
a delta would have predicted — four of the six first-round rulings changed clause text, and three added
behaviour that decomposes further (REQ-HBA-005 went 2 → 5, because AR-HBA-05 put three behavioural
rulings into what had been a one-sentence clause). It **refused to apply a predicted count as a delta**
and re-decomposed instead, which is why five assertions are not silently missing. Same treatment as the
first round: agent rulings, not the owner's.

### AR-HBA-07 — the re-raise condition is the accumulator ALONE *(closes AMB-10)*

**RULING:** the condition re-evaluated after a reset is the **accumulated persistence time alone**. It
is **not** gated on `PlantRunning`. Stop the plant, then reset, hopper still high, accumulator frozen
past the threshold → **the alarm re-raises.**

**Reasoning.** AR-HBA-05 implied the accumulator; REQ-HBA-001 with Q-HBA-04 implied cumulative
high-**and-running**. The separating sequence is *stop → reset*, and it is the most likely operator
sequence there is — an operator who sees an alarm generally stops the plant before acknowledging it.
Under the run-gated reading that sequence **clears a live fault and leaves it cleared**, which is
precisely the suppression AR-HBA-05 was written to forbid, arriving through a side door. The objection
that decided AR-HBA-05 decides this the same way.

**This completes one discipline, and it should be read as one story rather than three compatible
fragments:**

1. **The accumulator is evidence about the hopper** (AR-HBA-06) — it accrues on high-and-running,
   freezes on absent evidence, and is discarded only on positive evidence that the hopper cleared.
2. **`FaultReset` is an acknowledgement, not evidence about the hopper** (AR-HBA-05) — so it may not
   alter the accumulator.
3. **Therefore the alarm keys on the evidence, never on the plant's run state** (AR-HBA-07) — the run
   state gates what the accumulator *accrues*, and nothing else.

`PlantRunning` gates **accrual**. It does not gate **raising**, and it does not gate **re-raising**.

**CHANGED:** REQ-HBA-005 Text + Notes.

### AR-HBA-08 — the de-assert on reset is DON'T-CARE when the condition holds, REQUIRED when it has cleared *(closes AMB-09)*

**RULING:** split by case.

- **Condition still holds** → the momentary drop is **DON'T-CARE**. The observable claim is *the alarm
  is still true*. "Never went low" is **not** a failure.
- **Condition has cleared** → an observable **false** is **REQUIRED**, and it must persist.

**Reasoning.** The earlier "drops (if at all)" was unusable by a vector author: it made a passing and a
failing observation indistinguishable. Requiring the drop to be observable in the first case would be
worse than useless — under AR-HBA-07 the re-raise is immediate, so the drop is a sub-scan transient,
and asserting on it would force the assertion into a **Coincidence** requiring the program to record
scan numbers, to test something with **no operational consequence**. In the second case the opposite
holds: without an observable false, *"the reset worked"* and *"the reset did nothing"* are the same
observation, and that distinction is the entire content of REQ-HBA-005.

Both cases are now stated in the clause, so `005.A3` / `005.A5` can be worded as plain
`PersistentState` assertions rather than one of them being unobservable by construction.

**CHANGED:** REQ-HBA-005 Text (new by-case block) + Notes.

### AR-HBA-09 — below the filter time, the freeze applies *(closes AMB-11)*

**RULING:** plant stopped, hopper reading false, clear-debounce filter time **not** yet elapsed →
**freeze**. Nothing is discarded.

**Reasoning.** A discard requires a **debounced** clear. Under the filter time no clear has been
established, so there is nothing to discard, and the freeze is simply what still applies. One word —
but an uncovered case in a specification is exactly where two good-faith readings diverge silently, so
it is stated rather than left to be inferred. The enumerator's *"almost certainly freeze"* was the
right instinct and it was right to refuse to write "almost certainly" into a spec.

**Stated with it, because both AR-HBA-06 and this ruling depend on it:** the clear-debounce filter runs
**irrespective of `PlantRunning`**. If it did not, a debounced clear could never be established while
the plant was stopped and AR-HBA-06 would be unreachable.

**CHANGED:** REQ-HBA-003 Text + Notes, NEW-HBA-01 (scope completion).

### Opened by this round — flagged BEFORE the enumeration is stamped

**AR-HBA-07 collides with REQ-HBA-007's power-up split, and I do not think this one is mine to rule.**

REQ-HBA-007 makes the alarm latch **RETAIN** (survives a power cycle) while all transient timing state,
**including the accumulator**, is **non-retentive** and starts at zero. AR-HBA-07 makes the re-raise key
on the accumulator alone. Compose them:

> Power cycle with the alarm latched and the hopper genuinely still blocked. The alarm survives —
> correctly, per REQ-HBA-007. The accumulator does not; it is 0. The operator now presses `FaultReset`.
> The accumulator is below threshold, so **the alarm clears and does NOT re-raise**, and the hopper is
> still blocked. A fresh full threshold must accumulate before it alarms again.

That is a **live fault suppressed by a reset** — the exact outcome AR-HBA-05 and AR-HBA-07 both exist to
forbid — reached without either ruling being violated, because the power cycle destroyed the evidence
rather than the reset doing it. Both clauses are individually defensible and their conjunction is not.

I am **not ruling it**, for a reason: unlike AMB-07 and AMB-10, the fix is not a choice between two
readings already present in the register. Every available repair **adds a mechanism** — make the
accumulator RETAIN (contradicts REQ-HBA-007 and NEW-HBA-06 and changes power-up behaviour), or re-derive
the blockage condition live at power-up (adds a condition no clause describes), or accept the gap as the
documented cost of a non-retentive accumulator. That is a design decision affecting a signed REQ, so it
is the owner's.

**It is genuinely reachable** — any power cycle during a blockage puts the plant in this state — so it
should be settled rather than left. **Its assertion sits inside REQ-HBA-005 and REQ-HBA-007, both of
which this round already re-decomposes**, so ruling it now costs far less than ruling it after the
enumeration is stamped and citations exist.

Nothing else in AR-HBA-07/08/09 opens a further behavioural question that I can find.
