# GenProject1 readability retrospective — candidate simplicity rules (2026-07-16)

**Purpose.** Step 0 of the `docs/15-generation-pipeline.md` build order: walk the delivered
Kestrel Shredder build block-by-block, name what reads obtuse and why, and turn it into a
candidate C-6xx simplicity rule set for `docs/06-lad-conventions.md`. **Rules enter doc 06 only
after the project owner's accept/reject pass below** — until then nothing here is citable in a
review. This doc is also the working description of the standing reviewer-validation corpus.

**Inputs.** `ir/GenProject1/` at commit `b14be52` (21 files, exported+converted 2026-07-16);
mechanical baseline `converter review *.ir` (17 findings: 16 error, 1 warn — summarized in §2);
`docs/06-lad-conventions.md`; the S6 build record in `stage-gates.md`; patterns
`motor-dol`, `input-mapping`/`output-mapping`, `db-inputs`/`db-outputs`.

**How to respond.** Each candidate rule in §4 and each adjudication in §5 has checkboxes.
Tick one per item, add a line of reasoning where it says "owner"; anything accepted gets folded
into doc 06 (and the simplicity reviewer built against it) in the next batch.

---

## 1. The headline finding, stated honestly

The generated blocks are **not** the least readable code in this project — the real, imported
`FB_MotorFwdRevSystem` is, by a wide margin (§3.8: S/R webs over misspelled flag arrays,
five-branch mega-rungs, a telemetry network repeating a giant subexpression four times, TONR,
three alarm bits in one untitled-adjacent network). The generated blocks carry header comments,
step legends, why-comments, named per-purpose timers, plain-coil latches (C-403), constructed
edges (C-404), and one-alarm-one-network packing (C-501) — all things the real block lacks. The
mechanical review finds *zero* rule violations inside the two generated FBs; every inside-a-block
mechanical finding lands on the real block.

So "overly complex and obtuse" (the owner's judgment, which stands) is not about convention
violations — it's about things **no current rule names**: duplicated compound conditions,
interface sprawl, indistinguishable placeholder idioms, range predicates with silent future
behavior, and volume that may exceed what the spec asked for. That's exactly the gap the C-6xx
candidates below aim to close, plus a handful of items that need an owner ruling rather than a
new rule.

## 2. Mechanical baseline (converter review, Debug build)

- **Generated FBs (`FB_PusherControl`, `FB_ShredderSequencer`): clean.** No findings.
- **C-201 block header comment missing (error ×11):** `FC_ControlMain`, `FC_AlarmsMain`,
  `FC_Inputs`, `FC_Outputs`, `Main`, `DB_Input`, `DB_Output`, all 3 iDBs, `FB_MotorFwdRevSystem`.
  Genuine, fixable-now findings for the generated FCs/DBs; the real FB is read-only reference.
- **Real block only:** C-201 untitled network 5; C-301/C-501 (3 alarm bits `IO.Alarm.%X0–X2`
  packed into one network 15); C-406 ×2 + iDB (TONR `HrTotaliserTimer` — the exact "runtime
  counters" case C-406's own action item plans a site-standard retentive-timer FB for).
- UDTs/tag table: not checkable (Phase 1 has no TYPE/TAGTABLE rules — a known S4 scope edge).
- Tooling note: the **Release** converter build predates `review` entirely; the baseline ran on
  the Debug build. Worth a rebuild/publish step before reviewer skills depend on it.

## 3. Per-block walk

### 3.1 FB_PusherControl (177 readable lines, 13 networks)

Good: header step/mode legend; per-step networks grouping timer+fault+transitions (C-126 applied);
why-comments that earn their keep (N6's "never straight back into a forward stroke from an unknown
position"; N13's cold-start guard explanation).

Findings:

- **F-1 (→ C-601).** The 7-term cycle-start condition is duplicated verbatim between N4's
  count-reset (`MOVE(EN := (IO.Mode = 1 AND IO.HopperHighLevel OR IO.Mode = 2 AND
  IO.ManualCycleCmd) AND IO.Step = 0 AND IO.HomeLimit AND IO.Enable AND NOT IO.Blocked OR
  IO.FaultReset, …)`) and N6's start transition (same expression minus the `OR IO.FaultReset`
  tail). Two places to revise when the start condition changes, and the *near*-match makes the
  difference invisible. The same build shows the fix done right: `FB_ShredderSequencer` N1 names
  `StopCmd` once and reads it in every transition.
- **F-2 (→ C-605).** `UDT_PusherIO`: 31 members — commands, feedbacks, settings, faults, outputs
  interleaved, **zero member comments**, despite member-level comments being a capability the
  owner personally requested during this build's review ("a variable read far from where it's
  declared"). The HMI-facing interface (C-125/C-503) is currently undocumented at the member level.
- **F-3 (→ §5.1).** N3 "HMI Time Conversions": 7 MUL + 7 CONVERT funneled through one shared TEMP
  `Time` — the site's own MotorDOL idiom, faithfully copied (pattern `motor-dol` N6 and the real
  `FB_MotorFwdRevSystem` N7 both do it), but it is *batching by instruction kind separated from
  consumers*, which C-126's own text outlaws. Rule and practice disagree; needs a ruling, not a
  silent precedent. (Also see §6 tooling note: the IR's statement-kind ordering renders these
  ENO-chained pairs non-adjacently, which makes the shared-temp form look order-broken even when
  it isn't.)
- **F-4 (minor).** N4 packs debounce, hold-latch, edge, count-reset and count-increment into one
  network under a "one function" comment — it's at the edge of C-101's one-sentence test; the
  count reset+increment could live beside N5's threshold fault. Noted, not pressed: C-126 pulls
  the other way and the comment does explain it.

### 3.2 FB_ShredderSequencer (205 readable lines, 15 networks)

Good: same step-legend/header discipline; `StopCmd` named once (the C-601 exemplar); the
`AlwaysTrue` overcurrent placeholder is loudly commented in three places (the honest-gap pattern).

Findings:

- **F-5 (→ §5.2).** Reads `DB_Settings.*` directly inside the FB (N2 conversions ×10, N5
  threshold), while its sibling `FB_PusherControl` takes every setting through its UDT interface,
  MOVEd in by `FC_ControlMain`. Two sibling blocks, same problem, two policies — C-106's own
  argument ("sameness is a simplicity feature") applies to design idioms as much as rungs.
  Direct-read also couples the FB to this project's `DB_Settings` layout — the same
  usable-exactly-once trap C-127 exists to prevent, one level up.
- **F-6 (→ C-603).** Step-membership by range: `IO.MotorAutoStartCmd := IO.Step >= 30 AND
  IO.Step <= 50`; `IO.MotorPreStartDoneCmd := IO.Step >= 20`; `IO.RunDischargeConv := IO.Step >=
  20`. C-120 exists so a step can be inserted later without renumbering — but every inserted step
  then *silently* joins every range predicate that spans it. Membership should be a visible
  per-step decision (`= 30 OR = 40 OR = 50`), or the range's openness stated as intent.
  (`IO.Step <> 0` / `IO.InCycle := IO.Step <> 0` is fine — C-119 fixes idle=0.)
- **F-7 (noted, defensible).** N11's one-scan-stale read (`UpstreamEnableTimer.IN` sees
  `InfeedRunning` from the previous scan) is exactly the kind of subtlety that *must* carry a
  comment — and it does, including why it's harmless. Kept as a positive example of C-202 done
  right rather than a finding.

### 3.3 FC_ControlMain (81 lines, 7 networks)

Good: pure orchestration — wiring/CALL alternation, every cross-instance wire visible here and
nowhere else (C-127 as intended); reads top-to-bottom like a table of contents.

Findings:

- **F-8 (→ C-604).** N3 wires four constants into the motor FB with the same idiom the sequencer
  uses for its known-gap placeholder: `InHand := NOT AlwaysTrue`, `RecentStart := AlwaysTrue`,
  `InhibitMotor := NOT AlwaysTrue`, `HandReverse := NOT AlwaysTrue` — **no comment on any of
  them.** A reader cannot tell deliberately-constant ("no hand mode on this demo panel") from
  known-gap ("overcurrent compare unbuilt") without cross-referencing the sequencer's comments.
  The loud-TODO idiom only stays loud if it's *only* used for TODOs.
- **F-9 (→ C-201 fix).** No header comment (mechanical finding) — for the one block C-127
  designates as "the place a reader would actually look," a two-line header naming the
  orchestration order and the constant-wiring rationale would carry real weight.
- **F-10 (minor).** N7 is titled "Output Mapping" but is control-decision→buffer wiring; the
  mapping layer (C-304's reserved sense) is `FC_Outputs`. Retitle (e.g. "Buffer Writes") to keep
  the C-304 vocabulary unambiguous.

### 3.4 FC_AlarmsMain (38 lines, 9 networks)

Textbook C-501: one bit per network, slice-access, network title = HMI alarm text in C-505's
`<Equipment> — <fault> — <action hint>` shape. No C-504 suppression pairs exist (e.g.
`Motor_Fault` X0 vs `FTR` X1) — whether any alarm here is a known consequence of another is a
design judgment (Bucket C) flagged for the owner, not asserted as a defect. Missing header
comment (C-201).

### 3.5 Main / OB1 (4 networks)

`FC_Inputs → FC_ControlMain → FC_AlarmsMain → FC_Outputs` — C-110 order exactly. Two letter-level
deviations from C-109: the map FCs are called directly (no `FC_MapIOMain` wrapper — with one IO
source the wrapper would be a one-call layer) and there's no `FC_ComsMain` (nothing to talk to).
Reads as the right scale-down; §5.4 asks whether C-109's text should say so explicitly.

### 3.6 Mapping FCs + buffer DBs (pattern-grounded)

`FC_Inputs`/`FC_Outputs`/`DB_Input`/`DB_Output` follow the admitted `input-mapping`/
`output-mapping`/`db-inputs`/`db-outputs` patterns faithfully (Test/force array, spare points,
`AlwaysTrue` rail prefix — all site-real shapes). Two deviations:

- **F-11 (→ §5.3).** Buffer member naming: GenProject1 invented `Snake_Case` members
  (`Cycle_Start`, `Pusher_Home_Limit`) where the pattern's real example uses `PascalCase`
  (`HFLCRunning`, `IFCIsoFB`) — and doc 06 C-001 says UDT/variable members are **camelCase**
  (`run`, `fltHigh`), which *neither* the patterns, the real site blocks, nor the generated code
  actually follow. Three naming realities, one written rule. Needs one answer.
- **F-12 (cross-block, owner call).** No `DB_PLC`, no `Simulation` flag, no C-111 sim gating on
  the map FCs, and **no OB100 startup-reset block anywhere** — while `Step`, fault latches,
  `PressureHold`, `PumpEverDemanded` all live in RETAIN statics. C-124/C-403/C-305/C-111 all
  assume that machinery exists. On a power cycle mid-cycle, `Step` and the latches wake up
  retentive with no reset path but `FaultReset`. Possibly acceptable for a demo panel — but that
  call was never recorded. Either the machinery gets built, or the omission gets documented as a
  project-scope decision. (This is an enforcement gap for existing rules — the pipeline's
  `gen-architecture` checklist item — not a new C-6xx rule.)

### 3.7 DBs (settings/controls/alarms/analog)

`DB_Settings` is the best-documented data block in the project (C-307/C-308/C-309 header,
commissioning defaults, honest "genuinely unconfigured pending real numbers" note). `DB_Input`/
`DB_Output` lack header comments; `DB_Controls`/`DB_Alarms`/`DB_AnalogInput` have them.

### 3.8 FB_MotorFwdRevSystem — the read-only contrast case

Real site block, imported unmodified; **not proposed for any change** (owner's own working code,
and "user description outranks real data" — its quirks are facts to document, not errors to fix).
Recorded here because it calibrates the rule set — every candidate rule below should catch
something in *this* block too, or it isn't cutting at the real problem:

mega-rung `RunFwd`/`RunRev` (5 OR-branches × repeated factors, N2); telemetry N14 EN conditions
that repeat a ~9-term subexpression up to 4×; the N1 S/R web over `Pasue`/`StartTimer`/
`CycleDelay` with `RisingEdgeFlags[]`/`NegitiveSignalEdge[]` [sic] shared arrays; `ReverseDelay`
feeding `PauseTimeMS` (name mismatch across the conversion); TONR; 3 alarm bits in one network;
untitled N5; no header comment. A simplicity reviewer that flags the generated blocks but not
these would be validating the wrong bar.

---

## 4. Candidate simplicity rules (C-6xx) — owner accept/reject

Proposed section header for doc 06: **"Simplicity & readability"**, after Instructions/Alarms.
Severities follow doc 06's scheme; "bucket" is S4's checkability framing (A = single-block
mechanical, B = cross-block mechanical, C = human judgment).

### C-601 *(warn, bucket A)* — Name a condition used twice

A compound condition (≥3 terms) consumed by more than one network is written once to a named bit
(`StopCmd`, `CycleStartOk`) and read by name — never duplicated inline. One write, many reads;
the name is the documentation; divergence (the F-1 near-match) becomes impossible.
*Evidence:* F-1 vs `FB_ShredderSequencer`'s own `StopCmd`; the real block's N14 telemetry
repetition is the end state of not having this rule.
- [ ] Accept  - [ ] Accept with changes: ______  - [ ] Reject

### C-602 *(warn, bucket C)* — One-sentence rungs

C-101's test applied to a single coil: if one coil's expression can't be explained in one
sentence, split it into named intermediate bits or restructure. Soft numeric guide (not a hard
limit): >2 OR-branches or >6 contacts in one expression is the point to justify.
*Evidence:* real block N2/N14 (negative exemplar); generated blocks already mostly comply.
- [ ] Accept  - [ ] Accept with changes: ______  - [ ] Reject

### C-603 *(warn, bucket A)* — Step membership is enumerated, not ranged

Conditions over a stepped sequence's phase enumerate the steps they mean (`Step = 30 OR Step =
40 OR Step = 50`); ordered-range predicates (`>=`, `<=`, spans) are used only where "every future
step inserted in this span belongs here too" is the stated intent (comment). `Step <> 0` and
`Step = n` are always fine (C-119).
*Why:* C-120's insert-without-renumbering interacts with ranges silently — an inserted step 45
joins `>=30 AND <=50` with no visible decision.
*Evidence:* F-6.
- [ ] Accept  - [ ] Accept with changes: ______  - [ ] Reject

### C-604 *(error, bucket A)* — Constants and placeholders are visually distinct

Wiring a genuinely-constant value into a block input ("not fitted", "always permitted") uses a
named, documented source — a `Fitted`/mode setting (as `FB_PusherControl.IO.Fitted` already
does), or a commented constant — stating *why* it's constant. The `NOT AlwaysTrue` idiom **with a
comment naming the gap** is reserved for known-unbuilt placeholders (the sequencer's overcurrent
pattern). A bare, uncommented `AlwaysTrue`-derived constant is a finding: the reader can't tell
design from debt.
*Evidence:* F-8 vs the sequencer's loud placeholder; both currently read identically at the rung.
- [ ] Accept  - [ ] Accept with changes: ______  - [ ] Reject

### C-605 *(warn, bucket A for presence; C for quality)* — Interface members carry comments

Every member of an equipment FB's interface UDT (the C-115/C-125/C-503 HMI-facing surface)
carries a one-line member comment — role, units where numeric, and for settings the C-307 scope.
Member-level comments exist in the toolchain precisely because the owner asked for them
mid-Kestrel; an interface with 31 bare members (F-2) is the case that motivated it.
- [ ] Accept  - [ ] Accept with changes: ______  - [ ] Reject

### C-606 *(warn, bucket C)* — Justify size against the requirement

A generated block's header states, in one line per feature beyond the spec's literal ask, why it
exists (e.g. "trip *counting* with re-arm window: FuncDesc §x asks for jam handling; threshold
chosen because …"). Unrequested capability is a complexity cost like any other — the functional
review (docs/15 `review-functional`) flags logic that traces to no REQ, and this rule makes the
justification live *in the block* where a reader meets it.
*Evidence:* §7 — several pusher features (`PumpEverDemanded` cold-start guard, trip counting,
jog pre-start warning) are plausible but currently untraceable to the spec without the
requirements register.
- [ ] Accept  - [ ] Accept with changes: ______  - [ ] Reject

### C-607 *(warn, bucket B)* — One problem, one policy per project

When two blocks in one project solve the same recurring problem (settings access, edge storage,
time conversion), they solve it the same way; a deviation states its reason in the deviating
block's header. The specific instance behind this rule is adjudication §5.2 (settings access) —
the rule generalizes it.
*Evidence:* F-5.
- [ ] Accept  - [ ] Accept with changes: ______  - [ ] Reject

---

## 5. Adjudications needed (existing rules/practice in tension — not new rules)

### 5.1 "HMI Times" batch network vs C-126

The site's own proven idiom (MotorDOL, MotorFwdRev, both admitted patterns) batches every
Real-seconds→DInt-ms conversion into one up-front network — which is precisely "batching by
instruction kind separated from consumers" (C-126). Options:
- [ ] **(a) Codify the exception (recommended):** add to C-126: "Documented exception: the
  block-top HMI-time-conversion network (one MUL+CONVERT pair per timer, `<Name>MS` shadows) — one
  place to see every preset's unit conversion, site idiom predating this project."
- [ ] (b) Reverse the idiom: conversions move adjacent to their timers per C-126's letter —
  breaks with site practice and both patterns' real examples.
Owner: ______

### 5.2 Settings into an FB: through the interface, or direct DB_Settings reads?

`FB_PusherControl` = via UDT + caller MOVEs; `FB_ShredderSequencer` = direct reads. Proposal:
**kind-1-reusable equipment FBs take all per-instance parameters through their interface**
(C-127's spirit extended to settings — a reusable FB hardcoding `DB_Settings.PusherX` is
usable-once by another name); **plant-singleton FBs may read `DB_Settings` directly and say so in
the header**. Under that split both existing blocks are already right — the missing piece is each
stating which kind it is.
- [ ] Accept proposal  - [ ] All FBs via interface  - [ ] All FBs direct  - [ ] Other: ______

### 5.3 Member naming: C-001's camelCase vs universal PascalCase practice

C-001 says members are `camelCase` (`run`, `fltHigh`); the real site blocks, all admitted
patterns, and GenProject1 all use `PascalCase` (`RunFwd`, `HFLCRunning`, `HopperHighLevel`) —
and GenProject1 additionally invented `Snake_Case` buffer members (`Cycle_Start`) unlike the
pattern's own `PascalCase` example. Two calls: (1) revise C-001 to match practice (PascalCase
members)? (2) is `Snake_Case` in buffer DBs a deviation to fix at the next project, or an
accepted local style?
Owner: ______

### 5.4 C-109's letter vs the single-source scale-down

OB1 calls `FC_Inputs`/`FC_Outputs` directly (no one-call `FC_MapIOMain` wrapper) and omits
`FC_ComsMain` (no comms). Reads as correct scale-down; C-109's text could say "wrapper FCs appear
when there are ≥2 callees" so this stops being a judgment call.
- [ ] Amend C-109  - [ ] Keep as judgment  Owner: ______

### 5.5 Startup-state machinery (F-12)

No OB100 reset block, no `DB_PLC`, no C-111 simulation gating — with retentive `Step`+latches.
- [ ] Build it for GenProject1 (next S6 request)  - [ ] Document as accepted demo-panel omission
Owner: ______

### 5.6 FC_AlarmsMain suppression (C-504)

Is any of the 9 alarms a known consequence of another (candidate: `Motor_Fault` ↔ `FTR`)?
Bucket C — only the owner can say.  Owner: ______

## 6. Tooling follow-ups (not LAD rules)

- **IR statement-kind ordering obscures ENO-chained pairs:** the 7 MUL then 7 CONVERT rendering
  (F-3) forces the reader to reconstruct pairing and order from the sidecar. Options: render
  ENO-chained groups adjacently, or emit a pairing comment. Worth an `ir/SPEC.md` note either way.
- **Release converter build lacks `review`** — rebuild/publish before reviewer skills wrap it.
- C-201's author/revision fields aren't captured by the IR extraction (already flagged by the
  tool itself) — decide whether header-comment *content* is reviewable or only presence.

## 7. Open questions pending the requirements register

Functional traceability (does every feature trace to the spec, is every spec clause implemented)
was **not** performed — that's `review-functional`'s job and needs `gen-spec-analysis`'s
requirements register first. Features to trace when it exists: pressure-trip counting + re-arm
window; `PumpEverDemanded` cold-start guard; jog pre-start warning; reversal counting + window;
`Pusher_Local_Remote` (mapped in `DB_Input` but consumed nowhere — dead input or unimplemented
requirement?). The overcurrent `AlwaysTrue` placeholder (known gap) also stays open until typed
Compare capability exists.

## 8. What the generated code did right (keep doing)

Step/mode legends in headers; why-comments on every subtle decision (N6 recovery-first, N11
one-scan latency, N13 cold-start guard); named per-purpose timers with `DB_Settings` presets
(C-122/C-307); plain-coil latches with explicit `FaultReset` terms (C-403) instead of the real
block's S/R webs; constructed edges with dedicated named memory (C-404/C-402); hold vs fault kept
as two bits (C-123); one-alarm-one-network with C-505-format titles; `DB_Settings`'s honest
"unconfigured pending real numbers" documentation; the loud, thrice-commented overcurrent
placeholder rather than silent wrongness.
