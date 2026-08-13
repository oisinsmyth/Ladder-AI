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
- 🔴 **A bare parameter phrase always means the SPECIFIED value, never the block's own (AR-HBA-14).**
  Wherever this register says "the persistence threshold", "the debounce time", "the clear-debounce
  filter time" or any equivalent bare phrase, it means **the value in AR-HBA-03's bounds table** — the
  owner-specified one — and **never the value the implementation happens to hold**. A block whose preset
  disagrees with the table **fails the clause**; it does not redefine it. This applies to every clause
  and every assertion in the register, including all those written before AR-HBA-14 existed.
  **The general form, which is a repo-wide rule and not local to this register:** *** a requirement that
  references the implementation's own parameter is VACUOUS — the thing under test satisfies it by
  defining its own terms. *** Same defect as the narrowing repair (AR-HBA-13), reached from the other
  side: one drops a claim while looking intact, the other keeps the claim and empties it.
- 🔴 **Cite assertions by BEHAVIOUR, never by ordinal; and ordinals are APPEND-ONLY (AR-HBA-19/20).**
  Prose in this register refers to an assertion by **what it asserts** ("the reset coincident with the
  threshold crossing"), never by an ordinal like `005.A5`. A behaviour phrase carries no ordinal, hash
  or signal name and is therefore stable under re-decomposition **by construction**; an ordinal is
  stable only for as long as nobody inserts. Correspondingly: **assertion ordinals are assigned
  append-only — a re-decomposition never reorders within a clause — and the reading order of a clause's
  text implies NOTHING about its assertions' ordinals.** `REQ-HBA-002` is the historical example: it
  states limb 1 **first** but limb 1 is `002.A3`, appended so earlier ordinals would not shift.
  **Status after AR-HBA-22: RECORDED DEFAULT, no longer load-bearing** — since AR-HBA-21 removed the
  last ordinal reference, **no register prose depends on an ordinal** and reordering within a clause is
  genuinely free. ⚠️ **Scope of that claim, stated precisely: VERIFIED FOR THIS REGISTER, UNVERIFIED
  ELSEWHERE.** The gate rejects ordinal `Basis` citations **by shape**, so no vector can *cite* one —
  but **nothing stops an ordinal appearing in a vector's prose, a review finding or a wave report**, and
  none of those has been read. Keep append-only as the default until they have been.
- **Vocabulary: the coverage-bucket term is `UNCLASSIFIED`, never "uncovered".** `UNCLASSIFIED` is a
  **computed set difference** and the gate is `UNCLASSIFIED = 0`. At present **27 − 25 = 2, so the
  coverage gate SHOULD be failing, and it is correct that it is**; it clears when the two vectors land
  **and somebody classifies them** — an identity that is **not** the enumerator's, **not** the block
  author's, and **not** a vector author's for the two escape-hatch buckets. Where this register says a
  *case* is "uncovered" (REQ-HBA-003's freeze scope, AR-HBA-09, the AR-HBA-08 entry) it means a **gap in
  the specification's reasoning** and has nothing to do with coverage classification — the two senses
  are unrelated and must not be conflated.
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
- **Text:** Two claims, **both** of which hold. They are independent and neither replaces the other.
  1. **The unconditional threshold claim (restored by AR-HBA-13).** On a **first, uninterrupted high
     episode** — no debounced clear and no `FaultReset` since the accumulated persistence time was
     last zero — **the hopper-blockage alarm does not assert at any point before the full persistence
     threshold has been accumulated.** The threshold meant here is the **specified** one (AR-HBA-03's
     bounds table), **not whatever preset the implementation happens to carry**: a block whose preset
     is shorter than the specified threshold asserts early and **fails this clause**. Because the
     claim is about *accumulated* time, a run-state pause part-way through does not break the episode
     — accrual simply stops and resumes (NEW-HBA-01), and the claim still bites on the total.
  2. **The debounced-clear case (added by AR-HBA-04, unchanged).** If `Hopper_Level_High` undergoes a
     **debounced clear** — it goes false and stays continuously false for at least the clear-debounce
     filter time — before the accumulated persistence time reaches the threshold, no hopper-blockage
     alarm is raised for that high episode. A false reading shorter than the filter time is **not** a
     clear (Q-HBA-02) and does not, by itself, prevent the alarm.
- **Class:** alarm
- **Source:** Request (the testable inverse of REQ-HBA-001's continuous-high condition).
- **Notes:** Boundary requirement for the functional review; pairs with REQ-HBA-001. Threshold and
  filter values: see the "Commissioning-changeable bounds" table (AR-HBA-03).
- **Amended by AR-HBA-04 (agent ruling, not owner).** The original wording — "clears (goes false)" —
  literally contradicted Q-HBA-02, which says a brief false is not a clear. Q-HBA-02 is the later,
  owner-resolved statement and governs; this clause is reworded to match it rather than left as a
  contradiction for a downstream reader to resolve silently.
- **"Since the accumulator was last zero" is defined by AR-HBA-15 (agent ruling, not owner).** It means
  **from the most recent INSTANT at which the accumulator held zero, whatever put it there** — *not*
  from a zeroing **event**. The distinction is the whole ruling: after a power cycle the accumulator
  holds zero, and it **goes on holding zero through a subsequent `FaultReset`** until accrual actually
  begins — so the most recent zero-holding instant is **after** that reset, limb 1's "no `FaultReset`
  since…" is satisfied, and **limb 1 DOES bite inside AR-HBA-10's window.** No clause text changes; this
  fixes the referent of wording already stamped. See AR-HBA-15.
- **Restored by AR-HBA-13 (agent ruling, not owner) — a RESTORATION, NOT A REVERSAL of AR-HBA-04.**
  Read this before concluding the later ruling overturned the earlier one: **it did not.** AR-HBA-04's
  debounced-clear case stands **exactly as written** and is limb 2 above. What AR-HBA-13 does is put
  back limb 1 — the plain threshold claim this clause is *named* for — which AR-HBA-04 inadvertently
  narrowed away. Both limbs are now live. See AR-HBA-13.

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
  **What is observable after the reset, by case (AR-HBA-08)** — and this split applies to
  **both outputs** identically, `HopperBlockedInhibit` tracking `HopperBlockedAlarm` per REQ-HBA-008:
  - **The condition still holds** (accumulated time still at or past the threshold) — the alarm is
    **true** when next observed. Whether it went momentarily false in between is **DON'T-CARE**: the
    re-raise is immediate, so any drop is a sub-scan transient of no operational consequence, and
    "never went low" is **not** a failure.
  - **The condition has cleared** (the accumulated time is below the threshold — whether because a
    debounced clear discarded it, **or because a power cycle did**, AR-HBA-10) — the alarm goes
    **false and stays false**, and that false is **REQUIRED to be observable**. Without it "the reset
    worked" and "the reset did nothing" are indistinguishable, which is the whole point of this clause.
- **Class:** alarm
- **Source:** Request — "clear the alarm on FaultReset".
- **Notes:** `DB_Controls.FaultReset` is the shared operator reset already routed to the
  sequencer/pusher/motor faults in the corpus (`FC_ControlMain`); this FB uses the same signal.
  `FaultReset` does **not** touch the accumulated persistence time (AR-HBA-05) — the accumulator is
  governed only by hopper-level evidence, run state, and power-up (REQ-HBA-003, NEW-HBA-01, AR-HBA-06,
  AR-HBA-09, and REQ-HBA-007/AR-HBA-10 for the power-cycle discard).
- **Determined by AR-HBA-05 (agent ruling, not owner)** on three points the request left silent:
  - **Edge, not level.** The reset acts on the rising edge of `FaultReset`. A held-on reset does not
    keep the alarm cleared.
  - **Raise-dominant.** A reset coincident with the threshold crossing **does not consume that
    crossing**: the alarm **is asserted when next observed**, rather than the crossing being swallowed
    and the alarm failing to return. *(Reworded by AR-HBA-11 — this previously claimed the alarm "ends
    that evaluation asserted", a same-scan sequencing claim no assertion can falsify.)*
  - **The accumulated persistence time is untouched by reset** — it is neither zeroed nor frozen.
    Consequence, stated so it is testable: resetting while the hopper is still high and the plant
    still running produces an alarm that re-raises essentially at once, **not** one that re-arms
    from zero and stays quiet for another threshold's worth of time.
- **Re-raise condition fixed by AR-HBA-07 (agent ruling, not owner).** The re-raise keys on the
  **accumulator alone**, never on `PlantRunning`. So the sequence *stop the plant → reset → hopper
  still high, accumulator frozen past threshold* **re-raises**. See AR-HBA-07.
- **"Stays false" is defined by AR-HBA-18 (agent ruling, not owner).** In this clause's "condition has
  cleared" case, **"and stays false" means "stays false UNTIL THE RAISE CONDITION IS NEXT SATISFIED"** —
  not "for as long as the vector happens to observe", and not "forever". This is the **floor** in the
  post-power-cycle window: **the bounded return of the alarm after a post-power-cycle reset** is a `WITHIN` bound, i.e. an **upper** limit, which a **short**
  preset satisfies **more** easily rather than less, so nothing else there supplies a lower bound. A
  duration that depends on how long a test watched is not a specification — it would make the
  requirement's strength a property of the test. No clause text changes; this fixes the referent of
  wording already stamped. See AR-HBA-18.
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
  **Accepted consequence, stated rather than repaired (AR-HBA-10):** because the alarm survives and the
  accumulated persistence time does not, a `FaultReset` after a power cycle finds the accumulator at
  zero. **When `FaultReset` is asserted after a power cycle while the hopper is still blocked, the
  alarm clears and does not immediately re-raise; it re-raises WITHIN one further persistence threshold
  of accumulated high-and-running time.** The window this opens is bounded by that same threshold and
  closes with no further operator action. This is the accepted cost of a non-retentive accumulator, not
  an oversight — see AR-HBA-10.
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
- **Power-cycle/reset interaction ruled by AR-HBA-10 (agent ruling, not owner).** The gap is **accepted
  and stated**, with no mechanism added. It is bounded by the persistence threshold and self-clearing.
  **Depends on the accumulator remaining non-retentive** (this clause's Notes, NEW-HBA-06): if
  NEW-HBA-05 is ever settled in a way that makes the accumulated time retentive, AR-HBA-10 must be
  revisited. See AR-HBA-10.
- **Bookkeeping closed by AR-HBA-02 (agent ruling, not owner).** NEW-HBA-04 was listed OPEN while the
  revision it proposed was already present in this clause's Text. That was a contradiction about
  *whether a signed REQ had changed*, not about behaviour. NEW-HBA-04 is now RESOLVED; this clause's
  Text is unchanged by that closure.

### Output pairing

### REQ-HBA-008 — The inhibit output tracks the alarm output
- **Text:** `HopperBlockedInhibit` **tracks `HopperBlockedAlarm` at all times** — it asserts when the
  alarm asserts, stays asserted while the alarm is latched, de-asserts when the alarm is cleared by
  `FaultReset`, and re-asserts whenever the alarm re-raises. This holds at **every** transition the
  register describes: raise (REQ-HBA-001), latch (REQ-HBA-004, REQ-HBA-006), reset and re-raise
  (REQ-HBA-005, AR-HBA-07), power-cycle survival and the bounded post-power-cycle window
  (REQ-HBA-007, AR-HBA-10). It holds equally for **non**-assertion: while REQ-HBA-002 forbids the alarm
  before the threshold is accumulated, `HopperBlockedInhibit` is likewise not asserted (AR-HBA-13).
  There is no state in which one output is asserted and the other is not.
  **Observability follows the alarm's, identically** — AR-HBA-08's by-case split applies to this output
  in the same terms: a momentary drop is DON'T-CARE while the condition holds, an observable and
  persistent false is REQUIRED once it has cleared.
- **Class:** alarm
- **Source:** Q-HBA-05, which names the two outputs as a **pair**; tracking is what makes them a pair.
  Clause **added by AR-HBA-12 (agent ruling, not owner)** — see AR-HBA-12.
- **Notes:** "Tracks" is about **value**, not about wiring. The two remain distinct interface members so
  integration can route the alarm to annunciation and the demand to the stop independently (Q-HBA-05);
  this clause constrains what they read, not how they are consumed.

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

> **Conformance observation, 2026-08-13 — NOT A SOURCE FOR THIS TABLE.** Both specified values were read
> back from the scratch controller (`iDB_HopperBlockageMonitor` start values, TIA export):
> `BlockedTimeThreshold = T#60S`, `ClearDebounceTime = T#2S` — **both match**. Recorded as provenance
> for the D7 check only. 🔴 **The values in the table above remain owner-sourced (Q-HBA-01, NEW-HBA-03)
> and must never be re-derived from what the controller happens to hold** — that is the direction of
> inference AR-HBA-13 exists to keep open, since a claim written against the implementation's own preset
> is vacuously true of every implementation.

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
coincident reset and raise resolve **raise-dominant** — meaning, in observable terms, that the reset
**does not consume the threshold crossing** (reworded by AR-HBA-11). The **accumulated persistence time
is untouched by reset** — neither zeroed nor frozen.

**Reasoning.** AMB-01 and AMB-02 are one question, because the edge/level and dominance choices are
exactly what decides whether a reset can suppress a live fault.

- *Re-raise, not re-arm-from-zero.* A reset must not be able to suppress a fault that is still true.
  The re-arm-from-zero reading gives an operator a guaranteed quiet threshold's-worth of time on a
  hopper that is still blocked, which inverts the alarm's purpose. Re-raise is the conventional and
  the safe direction.
- *Edge, not level.* Under a level-sensitive reset a stuck-on or wired-on `FaultReset` pins the alarm
  off permanently and silently. An edge cannot do that.
- *Raise-dominant.* The same argument in the coincident case: a reset arriving exactly as the threshold
  is crossed must not swallow that crossing, or "a reset cannot suppress a live fault" fails at the one
  moment it matters most. **Stated as an observable** (AR-HBA-11), not as same-scan sequencing.
- *Accumulator untouched — and this is the point of consistency with AR-HBA-06.* The accumulator's
  discipline is **evidence about the hopper**: it accrues on high-and-running, freezes when running is
  lost, and is discarded on positive evidence that the hopper cleared. `FaultReset` is an operator
  acknowledgement of an alarm; it is **not** evidence about the hopper. So it must not be allowed to
  alter the accumulator. Zeroing on reset would be exactly the re-arm-from-zero behaviour rejected
  above, arriving by a side door.

**Testable consequence, stated so a vector can be written against it:** with the hopper high, the plant
running, and the alarm latched, a pulse on `FaultReset` yields an alarm that **is asserted when next
observed** — **not** one that stays clear for another threshold period.

> **Superseded wording, corrected 2026-08-13 by the pre-stamp consistency pass.** This paragraph
> originally read "drops (if at all) for essentially one evaluation and is asserted again". That is the
> exact phrasing **AR-HBA-08 ruled unusable by a vector author**, and leaving it here would have left
> the unusable form still standing in the rulings section after the clause text had been fixed — a
> second, stale source for the same assertion. The observable claim is *the alarm is true*; whether it
> dropped in between is DON'T-CARE per AR-HBA-08.

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

**Reconciled against Q-HBA-04, which is an OWNER answer and therefore outranks this ruling.** Q-HBA-04
says the window "accumulates only while the shredder/plant is actually running (**a full hopper at
standstill does not alarm**)". Read literally, that parenthetical could be taken to mean the alarm is
never *asserted* at standstill, which AR-HBA-07 would contradict. **It is read here as a statement about
accrual, not about the latch** — and Q-HBA-04's own first clause is explicitly about accumulation, so a
standstill hopper still never *causes* an alarm: no accrual happens, the threshold is never reached, and
nothing raises. AR-HBA-07 only concerns an alarm **already earned while running** and then acknowledged
at standstill. **Note also that the two can never diverge on the first raise:** the accumulator can only
reach the threshold while running, so the first raise always occurs while running, and AR-HBA-07 creates
no first-raise/re-raise asymmetry. If the owner intended the stronger reading — that the alarm is
suppressed at standstill — that overturns AR-HBA-07, and the owner's reading wins.

**CHANGED:** REQ-HBA-005 Text + Notes.

### AR-HBA-08 — the de-assert on reset is DON'T-CARE when the condition holds, REQUIRED when it has cleared *(closes AMB-09)*

**RULING:** split by case.

- **Condition still holds** → the momentary drop is **DON'T-CARE**. The observable claim is *the alarm
  is still true*. "Never went low" is **not** a failure.
- **Condition has cleared** → an observable **false** is **REQUIRED**, and it must persist. "Cleared"
  means the accumulated time is below the threshold, by **either** route that puts it there: a debounced
  clear (REQ-HBA-003) or a power cycle (REQ-HBA-007, AR-HBA-10). Both produce the same observable.

**Reasoning.** The earlier "drops (if at all)" was unusable by a vector author: it made a passing and a
failing observation indistinguishable. Requiring the drop to be observable in the first case would be
worse than useless — under AR-HBA-07 the re-raise is immediate, so the drop is a sub-scan transient,
and asserting on it would force the assertion into a **Coincidence** requiring the program to record
scan numbers, to test something with **no operational consequence**. In the second case the opposite
holds: without an observable false, *"the reset worked"* and *"the reset did nothing"* are the same
observation, and that distinction is the entire content of REQ-HBA-005.

Both cases are now stated in the clause, so **the re-raise after a reset with the condition still
holding** and **the reset coincident with the threshold crossing** can be worded as plain
`PersistentState` assertions rather than one of them being unobservable by construction.
*(De-cited by AR-HBA-19. The first phrase deliberately covers **both** the running and the stopped
re-raise — which is what this ruling always meant, and what the ordinal it replaced no longer reached.)*

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

### AR-HBA-10 — the power-cycle / reset gap is ACCEPTED and STATED, with no mechanism added

**RULING:** **accept the gap and state it explicitly in the clause text. Add no mechanism.**

**The gap.** REQ-HBA-007 makes the alarm latch RETAIN (survives a power cycle) while all transient
timing state, **including the accumulator**, is non-retentive and starts at zero. AR-HBA-07 makes the
re-raise key on the accumulator alone. Composed:

> Power cycle with the alarm latched and the hopper genuinely still blocked. The alarm survives —
> correctly. The accumulator does not; it is 0. The operator presses `FaultReset`. The accumulator is
> below threshold, so **the alarm clears and does not immediately re-raise**, on a still-blocked hopper.

**Reasoning — recorded in full, because a bare "accepted" reads as an oversight:**

1. **The exposure is bounded and self-clearing.** After the reset, accrual resumes and the alarm
   re-raises through *the same threshold that raised it the first time*. What AR-HBA-05 and AR-HBA-07
   forbid is a reset **permanently** suppressing a live fault. A bounded window that closes by itself,
   following a deliberate operator acknowledgement, is ordinary alarm practice and is **different in
   kind** — not a weaker version of the same thing.
2. **The power cycle created the exposure; the reset only reveals it — and REQ-HBA-007 already signed
   for it.** A non-retentive accumulator means *any* power cycle grants a fresh window on a still-blocked
   hopper, reset or no reset. That is REQ-HBA-007's stated design, not a consequence of AR-HBA-07.
3. **Both repairs cost more than the gap.** Retaining the accumulator contradicts a signed REQ *and* is
   independently wrong — accumulated *running* time carried across a power cycle asserts something about
   a plant state **nobody observed while the CPU was off**. Re-deriving the condition live at power-up
   invents a condition no clause describes, which is how a spec grows a mechanism that cannot be tested
   against anything.

**The honest residue, stated because it is real.** Without the reset, the latch holds and the inhibit
stays active. So the reset **does** open a window that would not otherwise exist. It is accepted
**because it is bounded by the threshold and closes automatically** — not because it is nothing.

**Made assertable rather than left as a note.** REQ-HBA-007's Text now carries it as a `WHEN … THEN …
WITHIN` with the bound explicit, so a vector can test that the alarm **does come back** and not merely
that it went away. If the bound were left implicit the enumerator would have to raise it again.

**Dependency.** AR-HBA-10 rests on the accumulator being **non-retentive** (REQ-HBA-007 Notes,
NEW-HBA-06). **NEW-HBA-05 is still OPEN**; if it is ever settled in a way that makes the accumulated
time retentive, this ruling must be revisited.

**CHANGED:** REQ-HBA-007 Text + Notes, REQ-HBA-005 Notes, AR-HBA-08 (both case definitions generalised
to name the power-cycle route).

---

## Third round (2026-08-13) — rulings AR-HBA-11…12

The re-decomposition returned **23 assertions** (from 13 at first enumeration) and found a **third**
combination-only collision. Same treatment: agent rulings, not the owner's.

### AR-HBA-11 — AR-HBA-05's same-evaluation claim is reworded as an observable *(closes AMB-12)*

**RULING:** amend AR-HBA-05's sentence to state what **the assertion that a reset coincident with the
threshold crossing does not consume it, so the alarm returns** already asserts. **Do NOT scope
AR-HBA-08.** Zero re-hashes. *(De-cited by AR-HBA-19.)*

**The collision.** AR-HBA-05 said the alarm *"ends **that evaluation** asserted"*; AR-HBA-08 rules the
momentary drop DON'T-CARE. Since an assertion must be falsifiable, **no assertion can carry AR-HBA-05's
literal same-evaluation claim** — leaving an unretracted second source stating a requirement the
enumeration cannot cover. Each ruling reads correctly alone; the collision exists only combined.

**Reasoning:**

1. **It was an implementation detail phrased as a requirement.** "Ends that evaluation asserted" is a
   claim about **internal same-scan sequencing**. Its **operational** content is carried in full by
   *a reset coincident with the threshold crossing does not consume it, and the alarm
   returns* — which is falsifiable, observable as ordinary persistent state, and is the thing anyone
   actually cares about. *(Ordinal deleted by AR-HBA-19: the italic description was already the
   de-cited form, so removing it leaves the argument reading unchanged.)*
2. **The alternative buys a `Coincidence` sibling** — an assertion requiring **the program to record
   scan numbers** — in order to test a consequence that is **already covered**.
3. 🔴 **And it would rest on an OPEN owner question.** Whether an author may *create* an observable
   value rather than surface an existing one is §2.6's unresolved item. ***Never take a spec decision
   that depends on an unresolved question when an equivalent decision does not.*** **That is the
   deciding argument** — not the cheaper diff, which merely agrees with it.

**CHANGED:** AR-HBA-05 RULING line + its raise-dominance reasoning bullet, REQ-HBA-005 Notes
(raise-dominant bullet).

### AR-HBA-12 — the inhibit output tracks the alarm at all times *(closes AMB-13)*

**RULING:** `HopperBlockedInhibit` **tracks `HopperBlockedAlarm` at all times** — raise, latch, reset
and re-raise. Stated as a **general clause (REQ-HBA-008)**, deliberately **not** as a special case
inside AR-HBA-10.

**Reasoning.** AR-HBA-10's bounded window releases **both** outputs, but only the alarm's return was
assertable — and the stop demand is **the operationally important half**, since it is the one that lets
the plant run on a blocked hopper. The enumerator was right to refuse to invent it. Q-HBA-05 names the
two as a **pair**, and *tracking is what makes them a pair*.

**Why general rather than case-specific.** An AR-HBA-10-only clause would leave the identical silence at
**every other transition** — raise, latch, hopper-clear-while-latched, ordinary reset. One general
statement makes AR-HBA-10's case fall out with no special-casing, and closes the rest at the same time.
The denominator is expected to reach **24**; that is the point of the ruling, not a cost of it.

**CHANGED:** new clause REQ-HBA-008; REQ-HBA-005 Text (AR-HBA-08's split marked as covering both
outputs).

### Fourth-collision re-read — one found, and closed here

Re-read after drafting both rulings, on the standing evidence that every previous round produced a
collision visible only once rulings were combined.

**Found: AR-HBA-12 × AR-HBA-08.** AR-HBA-08's by-case observability split is worded entirely about *the
alarm*. Once REQ-HBA-008 makes the inhibit track the alarm **at all times**, that tracking necessarily
includes the momentary drop AR-HBA-08 rules DON'T-CARE — so a vector asserting on the **inhibit's**
transient would have sat in exactly the untestable position AR-HBA-08 was written to remove, one output
over. AR-HBA-12 would have **propagated the AMB-09 problem to the second output** while appearing to
close a gap.

**Closed in place**, not filed: the split is now stated as applying to **both outputs identically**, in
REQ-HBA-005's Text and again in REQ-HBA-008's Text.

**Also checked and found clean:** that "tracks" is not read as "is the same bit" (REQ-HBA-008's Notes
keep the two distinct interface members, since Q-HBA-05 requires independent routing); that tracking
does not disturb REQ-HBA-006 (hopper clearing drops the accumulator, not the latch — so neither output
moves); and that AR-HBA-11's rewording leaves nothing else in the register depending on same-scan
sequencing.

## Fourth round (2026-08-13) — ruling AR-HBA-13

### AR-HBA-13 — restore REQ-HBA-002's unconditional threshold claim *(closes the vector author's dispute)*

**RULING:** restore the unconditional claim to REQ-HBA-002 **alongside** the debounced-clear case.
**Both, not either.** This is a **RESTORATION, NOT A REVERSAL** — AR-HBA-04's addition stands exactly as
written.

**The hole, and it was this register's own doing.** An independent vector author found that **no
assertion forbade the alarm raising EARLY on an uninterrupted first episode.** REQ-HBA-002 is *titled*
"No alarm below threshold", but after AR-HBA-04 its text conditioned entirely on a debounced clear. The
consequence, stated bluntly because it is the point:

> ***An under-scaled preset — `T#45S` instead of `T#60S` — left all 25 assertions TRUE.***

That is a whole defect class the coverage denominator could not see, and **the most ordinary
commissioning error there is**: a mistyped preset.

**AR-HBA-04 caused it.** That ruling reworded REQ-HBA-002 so it no longer literally contradicted
Q-HBA-02's debounce. It achieved that — and in doing so **narrowed the clause from a plain threshold
claim to a claim about the post-clear case only**, dropping the very thing the clause is named for.
**The clause's title survived and its content did not**, which is exactly why it went on reading as
covered. A narrowing repair is the dangerous kind: it removes a claim while leaving every sign that the
claim is still there.

**Why the threshold must be the SPECIFIED one.** Limb 1 is written against the threshold in AR-HBA-03's
bounds table, **not** against whatever preset the block carries. If it were written against the block's
own preset the claim would be vacuously true of every implementation — a `T#45S` block would assert "at
its threshold" and pass. Naming the specified value as the reference is what makes the mutation
detectable, and it is consistent with AR-HBA-03, which already says a vector "reads it from this table
and says which it used". **No number enters the clause sentence**, so assertion identity is untouched.

**Recorded because it is the mechanism working.** The gap was found by a **vector author**, not by
review of the register — and raised as a decomposition dispute rather than quietly worked around. That
is **D6 behaving as designed: the vector author is a second reader by construction**, and this is the
first time that has actually fired on this project. It is also the fourth distinct way this register has
produced a defect visible only from outside the reasoning that created it.

**CHANGED:** REQ-HBA-002 Text (limb 1 restored, limb 2 unchanged) + Notes; REQ-HBA-008 Text
(non-assertion made explicit).

### Consistency re-read — three interactions, all closed here

1. **REQ-HBA-008 × AR-HBA-13.** REQ-HBA-008's transition list named the *raise* clause but not the
   *non-raise* one, so the inhibit's early-assertion was covered only by the general "no state in which
   one is asserted and the other is not". **Made explicit** — the inhibit is likewise not asserted
   before the threshold. Without it, a `T#45S` block could have been read as failing REQ-HBA-002 on the
   alarm while nothing named the inhibit, which is the operationally worse half.
2. **NEW-HBA-01 × "uninterrupted".** Since accrual is run-gated, "uninterrupted" could have been read as
   requiring continuous *running*, which would make limb 1 untestable on any run that pauses. **Defined
   in the clause** as *no debounced clear and no `FaultReset` since the accumulator was last zero*, and
   the claim is stated against **accumulated** time — so a pause does not break the episode.
3. **AR-HBA-03 × limb 1.** Limb 1 references the bounds **table**, never a value, so the
   no-numbers-in-clause-text property and the zero-re-hash property both survive.

**Checked and found consistent:** limb 1 is the exact inverse of REQ-HBA-001 and the two pin the same
boundary from opposite sides; AR-HBA-07's re-raise is unaffected (limb 1 governs the first raise, from a
zero accumulator); and **AR-HBA-10 reinforces rather than conflicts** — the bounded post-power-cycle
window is bounded by *the same threshold* limb 1 protects, since a power cycle returns the accumulator
to zero and therefore starts a fresh "first episode".

## Fifth round (2026-08-13) — rulings AR-HBA-14…16

The re-decomposition landed at **27 assertions with ZERO re-hashes** — all 25 stamped texts
byte-identical, so neither vector set dangles. AR-HBA-13 achieved that by **adding** a claim rather than
moving one. **All three rulings below are likewise additive: no clause `Text` is touched.**

Its structural finding, which justifies the second new assertion and is worth stating plainly:

> *** An under-scaled preset asserts BOTH outputs early, and **the two directions of the output-tracking invariant** stay TRUE — because the two
> outputs still AGREE. A simultaneity claim is satisfied by a SYNCHRONISED ERROR. ***

REQ-HBA-008's amendment (AR-HBA-12, extended by AR-HBA-13) is what made the inhibit half nameable at
all.

### AR-HBA-14 — a bare parameter phrase means the SPECIFIED value *(closes AMB-16)*

**RULING:** one line in the Format section — wherever the register says "the persistence threshold",
"the debounce time" or any equivalent bare phrase, it means **AR-HBA-03's specified value, never the
value the block happens to hold.**

**Reasoning.** AR-HBA-13 was **the only place the register said "specified"**. Twenty-two other
assertions say *"the persistence threshold"* and nothing said whose. Read as the block's own preset,
several are far weaker than they look — the enumerator notes that **the post-clear re-accumulation while the plant is running** would otherwise also have
caught the under-scaled preset, post-clear**, and silently did not. One line **strengthens twenty-two
assertions without moving a character**, so the cost is zero re-hashes.

**The general form, recorded because it is now a repo-wide rule:** *** a requirement that references the
implementation's own parameter is vacuous — the thing under test satisfies it by defining its own
terms. *** This is the same defect as the narrowing repair (AR-HBA-13), arrived at from the other side:
**a narrowing repair drops a claim while leaving it looking intact; a self-referential parameter keeps
the claim and empties it.** Both are invisible to any check that reads the clause and asks only whether
it still says something.

**CHANGED:** Format section (one new bullet). **No clause text.**

### AR-HBA-15 — "since the accumulator was last zero" is an INSTANT, not an event *(closes AMB-15)*

**RULING:** it means **from the most recent instant at which the accumulator held zero, whatever put it
there** — not from a zeroing *event*. **Limb 1 therefore DOES bite inside AR-HBA-10's window.**

**The mechanism, stated because it is easy to get exactly backwards.** AR-HBA-10's sequence is: power
cycle (accumulator → zero) *then* `FaultReset`. Read as a zeroing **event**, the reset falls *after* the
event and limb 1's "no `FaultReset` since…" would be violated, so limb 1 would not apply. Read as an
**instant**, the accumulator **goes on holding zero through the reset** — nothing has made it accrue yet
— so the most recent zero-holding instant is **after** the reset, the precondition is satisfied, and
limb 1 applies to the accrual that follows.

**Why that reading.** After a power cycle the accumulator accrues from zero, and that **is** a first
uninterrupted episode by any ordinary reading. The alternative leaves an under-scaled preset invisible
in **exactly the post-power-cycle case** — the one AR-HBA-10 itself identified as risky — reopening the
hole AR-HBA-13 closed, one scenario over. It also matters that **the bounded return of the alarm after a
post-power-cycle reset is a `WITHIN` bound, i.e. an
upper limit, which a SHORT preset satisfies more easily rather than less**; nothing else there supplies
a lower bound, so without limb 1 biting, that window has no floor at all.

**Keeps the count at 27.** The alternative reading needs two further assertions to cover what limb 1
would then fail to reach.

**CHANGED:** REQ-HBA-002 Notes (definition). **No clause text.**

### AR-HBA-16 — title-vs-assertion is a REPORTED SIGNAL ONLY *(closes AMB-17)*

**RULING:** adopt "a clause title that none of its assertions supports" as a **reported signal only**. It
is **flagged for a human to look at**. It is **never** a source of an assertion and **never** closes a
gap by itself.

**The enumerator was right to refuse it, and that refusal is upheld, not overridden.** It observed that
such a title is a cheap smell test — **this register had one for four rounds** (REQ-HBA-002, "No alarm
below threshold", whose content AR-HBA-04 had narrowed away) — and declined to adopt it as a rule,
because **deriving assertions from titles stops the denominator being spec-derived.** That is correct:
a title is a label an author chose, not a requirement anyone stated, and an enumeration that reads
titles is reading the author's filing rather than the specification.

> 🔴 **To any future enumerator: this is NOT licence.** You may **report** that a clause's title has no
> supporting assertion. You may **not** write an assertion to make a title true, treat a title as
> evidence that a claim exists, or count a title toward coverage. If a title implies a claim the clause
> text does not make, **that is a finding to raise as a dispute** — exactly as the vector author did
> with REQ-HBA-002 — and the repair is an amendment to the **clause text**, made by a ruling, never an
> assertion invented to fit the heading.

**CHANGED:** this entry only. **No clause text, no Format change.**

### Combination re-read — four interactions, all closed here

AR-HBA-14 touches every clause at once, which is the profile of an edit that interacts.

1. 🔴 **AR-HBA-14 × AR-HBA-13 — a "tidying" trap, and the most likely future damage.** AR-HBA-13's limb 1
   says "the **specified** one" explicitly. After AR-HBA-14 that word is *redundant*. **It must NOT be
   removed.** Deleting it would move stamped text and dangle both vector sets' citations, to buy
   nothing. **Redundancy here is deliberate and load-bearing; do not tidy it.**
   > 🔴 **The hazard reaches further than the stamped text (enumerator, carried here 2026-08-13;
   > reference form fixed by AR-HBA-21).**
   > **Every assertion whose own hashed text contains the word "specified" — at present two — survives
   > on its own words if AR-HBA-14 is ever overturned or narrowed.** Every other assertion depends on the
   > Format line, which is a **weaker load path than the sentence itself**. **Do not delete the word from
   > either, even though they are unstamped and deleting it would be free today.** *** That is the worst
   > kind of tidy — costless at the moment you do it. ***
   >
   > *** THIS REFERENCE IS DELIBERATELY SELF-FALSIFYING, AND THAT IS THE ENTIRE POINT. *** It identifies
   > those assertions by a **property of their wording** — not by name, ordinal or claim. **Delete the
   > word and the sentence stops naming that assertion, and the count stops matching.** It therefore
   > **breaks visibly at the moment the protection is breached, instead of outliving it.** It is
   > **set-defined, not count-defined**, so any future assertion needing the same protection is picked up
   > automatically and no one has to remember to add it.
   >
   > **Why NOT a behaviour phrase here**, though AR-HBA-19 mandates that form everywhere else: a
   > behaviour phrase identifies an assertion by its **claim** — and **a claim is exactly what a
   > rewording preserves.** A behaviour phrase would go on pointing at these two *after* the word was
   > deleted, so *** the note would survive the deletion it exists to prevent, going on asserting a
   > protection that no longer exists *** — the wrong-pointer defect (AR-HBA-17) **inside the note
   > written to stop the tidy.**
   >
   > *Orientation only, never the identifier: these are the early-assertion floor, one for the alarm and
   > one for the stop demand.*
   >
   > **The general lesson, worth carrying past this register:** *** the ordinal never encoded the reason;
   > it just pointed at two assertions that happened to have it. ***
2. **AR-HBA-14 × AR-HBA-03's bounds table.** The table's "Where it lives at runtime" column says "FB's
   own `TIME` tunable", which could be misread as making the block authoritative. It is not: that column
   says where the specified value is **installed**, not where it is **defined**. **If the block's
   tunable disagrees with the table, the table wins and the block is wrong** — which is precisely what
   D7's read-back checked.
3. **AR-HBA-15 × REQ-HBA-003.** A debounced clear also puts the accumulator at zero, so limb 1 restarts
   after every clear, not only on the very first episode ever. **That is complementary to limb 2, not in
   conflict with it:** limb 2 governs the episode *ending* in a clear, limb 1 governs the fresh accrual
   *following* it. No new assertions — limb 1's existing text is already generic over "the accumulator
   was last zero".
4. **AR-HBA-15 × AR-HBA-05.** Because a `FaultReset` does **not** zero the accumulator (AR-HBA-05), a
   reset *mid-episode* does break limb 1's precondition and limb 1 steps aside. **That leaves no hole:**
   the case is covered by **the reset that clears once the condition has gone** (and the stop demand with it).
   > 🔴 **Corrected by AR-HBA-17.** This originally read *"AR-HBA-07 governs that case"*. **It does
   > not.** AR-HBA-07 lives in **the two re-raise assertions, running and stopped**, and **both are triggered by the accumulator being at
   > or past the threshold** — whereas a mid-episode reset is by definition **below** threshold, which is
   > **the reset that clears once the condition has gone** is its case. The conclusion (limb 1 steps aside, no hole) was and remains correct;
   > only the guard named was wrong. See AR-HBA-17 for why a wrong pointer is worse than a missing one.

**Checked and found clean:** AR-HBA-14 strengthens rather than alters AR-HBA-03 (it pins the *referent*
of an arrangement that was already "value beside, quantity in the text"); AR-HBA-16 adds no assertion and
so cannot move a count; and none of the three rulings touches a clause `Text`, so the 25 stamped
assertion texts remain byte-identical and **neither vector set dangles.**

## Sixth round (2026-08-13) — rulings AR-HBA-17…18

**27 stands.** No assertion decomposes differently under AR-HBA-14/15/16 and no wording change is
proposed to any of the 27. Both rulings below are **additive / notes-level**: **no stamped assertion
text moves.**

This round's defect was **in a ruling's REASONING, not its conclusion** — which is why the re-read
below reads the reasoning rather than the rulings.

### AR-HBA-17 — AR-HBA-15's item 4 named the wrong guard *(closes the enumerator's combination finding)*

**RULING:** correct item 4 to name **the reset that clears once the condition has gone** (and the stop demand with it). The conclusion stands — limb 1 does step
aside and there is no hole — but the guard that actually covers the case is the one to name.

**The error.** Item 4 said that when a `FaultReset` arrives **after accrual has begun** (say 10 s of
60 s), *"AR-HBA-07 governs that case."* ***It does not.*** AR-HBA-07 lives in **the two re-raise assertions, running and stopped**, and
**both are triggered by the accumulator being at or past the threshold.** A mid-episode reset is by
definition **below** threshold — **the reset that clears once the condition has gone** is its case.

**Why this is worse than a cross-reference slip:**

> *** A vector author following item 4 would test the past-threshold scenario and never test the
> below-threshold one — the only one where the floor is at risk. ***

**A wrong pointer in a ruling's reasoning is worse than a missing one: it satisfies the reader that the
case is handled.** A missing pointer invites a look; a confident wrong one closes the question. This is
the same family as the narrowing repair and the self-referential parameter — *a defect that leaves every
outward sign of correctness intact* — and it is now the third distinct member of that family found in
this register.

**Found by the enumerator's combination check, NOT by re-reading the ruling.** Recorded because it says
where the check has teeth: the ruling had been read several times, including by me in the round that
wrote it, and re-reading did not surface it. **Cross-referencing a ruling against the assertions it
claims to rely on is a different operation from reading it**, and only the former caught this.

**CHANGED:** AR-HBA-15's combination re-read, item 4. **No clause text, no assertion text.**

### AR-HBA-18 — "stays false" means until the raise condition is next satisfied *(closes AMB-18)*

**RULING:** **"stays false until the raise condition is next satisfied."** Settled as a **notes-level
definition**, not a rewording.

**Reasoning.** With item 4 corrected, the floor in that window is carried by **the clearing reset's persistence claim** and nothing else — **the bounded return of the alarm after a post-power-cycle reset** is a `WITHIN` bound, an **upper** limit, which a **short** preset
satisfies **more** easily rather than less. Three readings were available:

| reading | verdict |
|---|---|
| until the raise condition is next satisfied | **RULED** — covered, and independent of the test |
| bounded by the vector's observation window | **rejected — vector-dependent**, missed by any vector that stops observing early |
| unbounded ("forever") | rejected as absurd — the alarm is *supposed* to return |

**A duration that depends on how long a vector happened to watch is not a specification: it makes the
requirement's strength a property of the test.** That is the same objection as AR-HBA-14's — a claim
whose terms are supplied by the thing being measured — one level out, with the *test* rather than the
*implementation* supplying them.

🔴 **Settled as a definition and not a rewording, deliberately.** **The clearing reset's persistence claim** is **stamped and cited by two
vector sets**: a rewording dangles both, a definition costs **zero**. AR-HBA-15 was settled the same way
for the same reason, and this is now the established route for fixing a *referent* after stamping.

**CHANGED:** REQ-HBA-005 Notes (definition). **No clause text.**

### Combination re-read of the REASONING — a defect class found, and CLOSED in the seventh round

Read the ruling **prose** this round, not the rulings, and specifically every **assertion-ID citation**
in it — the operation that caught AR-HBA-17. **Six existed.** Three were current; three were `005.A*`
citations from earlier rounds, never re-verified as the count went 13 → 19 → 23 → 25 → 27.

**Referred to the enumerator rather than guessed at** — assertion identity is its authority, and
inventing a correction here would have been the wrong-pointer defect committed while fixing it.

> ✅ **RESOLVED, and the resolution is the finding.** The enumerator confirmed **all three were still
> correct** — and that *"every addition across all eight clauses in five issues was an **append**. No
> ordinal has ever shifted. **They survived because nothing ever inserted, not because anything
> checked them.**"* Every ordinal in this register has since been **de-cited** (AR-HBA-19) and the
> append-only discipline they were silently relying on has been **written down** (AR-HBA-20).

## Seventh round (2026-08-13) — rulings AR-HBA-19…20

### AR-HBA-19 — every ordinal citation is replaced by a behaviour phrase

**RULING:** de-cite **every** assertion-ordinal citation in this register, using the enumerator's
substitution table **verbatim**. Assertion identity is the enumerator's; the register does not
paraphrase it.

**Nothing in the enumeration moves.** No assertion text changes, no re-hash, no dangled citation — the
edits are entirely to *this register's prose*.

**Why a behaviour phrase is the stable form.** It carries **no ordinal, no hash and no signal name**, so
it is stable under re-decomposition **by construction** — where an ordinal is stable only for as long as
nobody inserts.

🔴 **One substitution is a strict improvement, not merely a stabilisation.** AR-HBA-08's `005.A3`: at
the third issue the re-raise **split** into a running case and a stopped case. AR-HBA-08's DON'T-CARE
covers **both**, but the ordinal named only **one of them** — *narrowed silently, without ever becoming
wrong.*

> *** A RE-VERIFICATION THAT ONLY ASKS "DOES THIS ORDINAL STILL EXIST?" WOULD PASS IT. ***

The behaviour phrase — *the re-raise after a reset with the condition still holding* — re-widens to
both, repairing a narrowing that no existence check could have detected. **That is the narrowing repair
(AR-HBA-13) reappearing in a citation rather than in a clause.**

**And the cheapest instance of the ruling was already present.** AR-HBA-11's reasoning **already
contained its own de-cited form** — the behaviour description sat in italics immediately after the
ordinal — so deleting the ordinal left the argument reading unchanged. The enumerator's observation is
worth keeping: *** the de-cited form is what people write naturally when they are actually thinking
about the behaviour. *** The ordinal is what gets added afterwards, to look precise.

**CHANGED:** ten citations across AR-HBA-08, AR-HBA-11, AR-HBA-14, AR-HBA-15, AR-HBA-17, AR-HBA-18 and
the fifth-round intro. **No clause text, no assertion text.**

### AR-HBA-20 — assertion ordering within a clause is APPEND-ONLY

**RULING:** ordinals are assigned **append-only**. A re-decomposition **never reorders** existing
assertions within a clause. **The reading order of a clause's text carries no implication whatever about
its assertions' ordinals.**

**The fourth member of the defect family, and the sharpest:**

> *** A CORRECTNESS PROPERTY, CORRECTLY STATED, THAT MAKES AN UNSAFE OPERATION LOOK SAFE — BECAUSE THE
> PROPERTY DOES NOT COVER THE THING THAT ACTUALLY DEPENDS ON IT. ***

`assertion-enumeration.md` §3.2 guarantees an ID depends on **nothing positional**. That is true and
load-bearing — but it *reads* as *"order does not matter"*, and order **does** matter, to every ordinal
written in prose. So an enumerator may reorder within a clause and **no ID moves, no text moves, no gate
fires, and every ordinal citation silently retargets.** *The guarantee that makes the ID scheme sound is
exactly what makes the reorder look free.*

**The exposure was real, not theoretical:** the enumerator followed append-only for five issues **to
keep its re-hash reports small**. No rule required it. It is written down nowhere. Every ordinal
citation in this register survived on that unstated habit.

🔴 **And the temptation is LIVE in this register right now — do not tidy it.**

> **REQ-HBA-002 states limb 1 FIRST in its text, but limb 1 is `002.A3`** — appended so that `002.A1`
> and `002.A2` would not shift. **The ordinal order therefore disagrees with the clause's own reading
> order, which looks exactly like an untidiness worth fixing.** It is not. Renumbering to match the
> prose would retarget every citation to that clause while moving no text and firing no gate.
> **Same treatment as the "specified" hazard, and for the same reason: *costless at the moment you do
> it*.**

**Note the interaction with AR-HBA-19, which is what makes the pair worth having:** once **no prose
depends on an ordinal**, reordering genuinely *is* free and this rule stops being load-bearing.
De-citing is what earns the freedom the §3.2 guarantee appeared to promise. Until every artifact that
cites this enumeration is de-cited, append-only is the thing holding it up.

**CHANGED:** this entry, plus the Format section's referencing rule. **No clause text.**

### Combination read — a rule ABOUT the register's own referencing

A new kind of edit for this register, so read for what it interacts with rather than what it says.

1. **AR-HBA-19 × AR-HBA-17.** AR-HBA-17 corrected a pointer *to another ordinal*; AR-HBA-19 removes the
   category. **The correction survives de-citing** because it was a claim about *which behaviour*
   governs, not about which number — re-checked, and the corrected item 4 now names the behaviour
   directly. **De-citing does not erase the AR-HBA-17 finding; it removes the mechanism that produced
   it.**
2. **AR-HBA-19 × AR-HBA-14/18's residual `002.A3` / `008.A3`.** The "specified" hazard note names those
   two **to identify assertions whose wording must be preserved** — an *identifying* use, not a
   *citing* one, and the enumerator supplied **no substitution** for them at the time. They were the
   register's only remaining positional references and therefore the only place AR-HBA-20's discipline
   was still doing real work.
   > ✅ **RESOLVED by AR-HBA-21 (eighth round).** They were **not** replaced with a behaviour phrase —
   > that would have been actively wrong, because the note's subject is **how those assertions are
   > worded**, and a behaviour phrase identifies by **claim**, which a rewording preserves. They are now
   > identified by the **property** ("every assertion whose own hashed text contains the word
   > *specified*"), which is **self-falsifying**. **No register prose depends on an ordinal any more**,
   > which is what let AR-HBA-22 demote AR-HBA-20 to a recorded default.
   > *(This paragraph itself had gone stale between rounds — corrected in the eighth-round read, and
   > noted as one more instance of the class: a cross-reference that describes a state the register has
   > since left.)*
3. **AR-HBA-20 × REQ-HBA-002's structure.** Recorded above as the live case. Worth noting that the
   append-only discipline and AR-HBA-13's *"restoration, not reversal"* pull the same way: limb 1 is
   both **stated first** and **appended last**, and *both* facts are deliberate.

**Checked and found clean:** no substitution altered a ruling's conclusion — each replaced a reference,
not an argument; AR-HBA-19 introduces no new claim about behaviour, so the count stays **27**; and
neither ruling touches a clause `Text`.

**Checked and found clean:** AR-HBA-18's definition does not disturb AR-HBA-08's by-case split (it
supplies a duration to the "cleared" case, which that split leaves open); it does not interact with
AR-HBA-10's `WITHIN` bound except by supplying the floor that bound lacks; and neither ruling this round
touches a clause `Text`, so the 27 remain byte-identical.

## Eighth round (2026-08-13) — rulings AR-HBA-21…22

Closes the residual left open by AR-HBA-19: the two ordinal references in the "specified" hazard note.
**The answer was not the behaviour phrase used everywhere else — a behaviour phrase would have been
actively wrong there**, for a better reason than the one raised.

### AR-HBA-21 — identify them by the PROPERTY, not by name, ordinal or claim

**RULING:** replace the ordinal reference with a **property-based, self-falsifying** one: *every
assertion whose own hashed text contains the word "specified" — at present two*.

**Why a behaviour phrase would have been wrong here.** The note's subject is **not what those two
assertions claim**; it is **how they are worded** — they carry the word "specified" in their own hashed
text. A behaviour phrase identifies an assertion by its **claim**, and *** a claim is exactly what a
rewording preserves. *** So if someone later deleted the word, the phrase would still point at them and

> *** THE NOTE WOULD GO ON ASSERTING A PROTECTION THAT NO LONGER EXISTS — THE WRONG-POINTER DEFECT
> (AR-HBA-17), INSIDE THE NOTE WRITTEN TO STOP THE TIDY. ***

**Why self-falsifying is the right property, and it is the entire point.** Delete the word and the
sentence **stops naming that assertion, and the count stops matching**. The reference therefore **breaks
visibly at the moment the protection is breached, instead of outliving it.** Every other reference form
this register has used fails in the opposite direction — it keeps reading correctly after the thing it
protects is gone.

**Set-defined, not count-defined.** "At present two" is an orientation aid, not the definition. Any
future assertion that comes to carry the word is picked up automatically, and nobody has to remember to
extend the note.

**An orientation gloss is permitted and is marked as such** — *the early-assertion floor, one for the
alarm and one for the stop demand* — **never as the identifier.** No ordinal, no hash, no signal name.

**The general lesson, recorded because it outlives this register:** *** the ordinal never encoded the
reason; it just pointed at two assertions that happened to have it. *** A reference that carries the
*reason* can be checked against reality; one that carries only a *pointer* can only be checked against
existence — and AR-HBA-19 already showed an existence check passing a silently narrowed reference.

**CHANGED:** the "specified" hazard note in AR-HBA-14's combination re-read item 1. **No clause text, no
assertion text.**

### AR-HBA-22 — AR-HBA-20 demoted to a recorded default, with the claim's SCOPE stated

**RULING:** keep AR-HBA-20's sentence as a **recorded default, no longer load-bearing**. Record that
*"no prose depends on ordinals"* is *** VERIFIED FOR THIS REGISTER, UNVERIFIED ELSEWHERE. ***

**Why it can be demoted.** After AR-HBA-21 **no register prose depends on an ordinal**, so reordering
within a clause is genuinely free and append-only stops carrying weight. That is the outcome AR-HBA-20
predicted: *de-citing earns the freedom §3.2 appeared to promise.*

⚠️ **Why the claim must be scoped rather than stated generally.** It is verified **for the register
only**. The gate rejects ordinal `Basis` citations **by shape**, so no vector can *cite* an ordinal —
**but nothing stops an ordinal appearing in a vector's prose, a review finding or a wave report**, and
none of those has been read.

> **A scope-less claim here would be exactly the guarantee-that-licenses-the-damage shape, one turn
> later** — a correctness property, correctly stated, that makes an unsafe operation look safe because
> it does not cover the thing that actually depends on it. **This register has already produced four
> members of that family; writing a fifth into the ruling that retires the fourth would be a poor
> joke.** Append-only therefore stands as the default until the other artifacts have been read.

**CHANGED:** Format section (AR-HBA-20's bullet — status + scope). **No clause text.**

### Combination read — a self-falsifying reference and a demotion

Both are new kinds of edit for this register.

1. **AR-HBA-21 × AR-HBA-19.** These prescribe **different** reference forms, and that is deliberate, not
   an inconsistency: AR-HBA-19's behaviour phrase is right when the subject is **what an assertion
   claims**; AR-HBA-21's property reference is right when the subject is **how an assertion is worded**.
   **The rule that reconciles them: identify by whatever the note is actually about, so the reference
   fails when its subject does.** Recorded here because a future reader will otherwise see AR-HBA-21 as
   an exception to AR-HBA-19 and "correct" it — which would reinstate exactly the defect AR-HBA-21
   removes.
2. **AR-HBA-22 × AR-HBA-20.** Demotion is **not** deletion. AR-HBA-20's sentence stays, and its live
   example stays, because the scope caveat means the discipline is still the default. **Do not read
   "no longer load-bearing" as "no longer applies."**
3. **AR-HBA-21 × the UNCLASSIFIED vocabulary.** The note calls the two assertions "unstamped", which is
   about **citation state**, while `UNCLASSIFIED` is about **coverage classification**. Both are true of
   them right now and they are different facts; neither is a synonym for the other, and the note is
   about a third thing again — their **wording**. Three orthogonal properties of the same two
   assertions, which is precisely why identifying them by the right one mattered.

**Checked and found clean:** no substitution altered a conclusion; AR-HBA-21 introduces no claim about
block behaviour, so the count stays **27**; the register's three plain-English uses of "uncovered" refer
to **gaps in specification reasoning**, not to coverage buckets, and are correct as written — the
Format section now says so explicitly so the two senses cannot be conflated later.

## Pre-stamp consistency pass (2026-08-13)

The whole register was read **once, end to end, as a single document**, specifically for interactions
between the ten rulings — the failure mode being that both collisions found so far (AMB-10, and the
AR-HBA-10 gap) were **invisible clause-by-clause and only appeared when rulings were combined**.

**Result: four items found, all resolved above. No unresolved inconsistency remains.**

| # | finding | disposition |
|---|---|---|
| 1 | **AR-HBA-05's "testable consequence" still read "drops (if at all)"** — the exact wording AR-HBA-08 ruled unusable. The *clause* had been fixed; the *ruling* still carried the old form, leaving a second, stale source for the same assertion | **Fixed** — rewritten, with the supersession noted in place |
| 2 | **AR-HBA-08's "condition has cleared" named only the debounced-clear route.** AR-HBA-10 adds a second route to the same state (power cycle), which would have read as uncovered | **Fixed** — both case definitions generalised |
| 3 | **Q-HBA-04's parenthetical** ("a full hopper at standstill does not alarm") could be read literally as contradicting AR-HBA-07 | **Reconciled in AR-HBA-07**, and flagged: Q-HBA-04 is an **owner** answer and outranks the ruling if the stronger reading was meant |
| 4 | **AR-HBA-10 depends on an OPEN item** — NEW-HBA-05's mechanism choice, via accumulator retentivity | **Recorded** as an explicit dependency at both AR-HBA-10 and REQ-HBA-007 |

**Checked and found consistent** (stated rather than assumed):

- **The latch's discipline is coherent across REQ-HBA-004 / 006 / AR-HBA-05 / 07.** Set when the
  accumulator reaches the threshold; cleared **only** by a `FaultReset` edge; re-set immediately if the
  accumulator is still at or past the threshold. A debounced clear drops the accumulator but **not** the
  latch, which is exactly REQ-HBA-006.
- **No first-raise / re-raise asymmetry.** The accumulator can only reach the threshold while running,
  so the first raise always occurs while running; AR-HBA-07's ungated re-raise therefore never diverges
  from REQ-HBA-001's raise.
- **The accumulator has exactly four governors and they do not overlap:** accrue (high-and-running),
  freeze (AR-HBA-06/09), discard (debounced clear), zero (power cycle, AR-HBA-10). `FaultReset` is
  deliberately **not** among them (AR-HBA-05).
- **AR-HBA-01/02/03 are structural, not behavioural**, and interact with nothing above.
- **The bounds table and the signal-name property** (AR-HBA-03, Format) remain true after AR-HBA-07…10:
  no ruling put a number or a signal name into a clause sentence.
