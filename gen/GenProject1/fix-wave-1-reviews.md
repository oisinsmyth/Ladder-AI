# Fix wave 1 — check-stage reviews (2026-07-17)

Three fresh-context reviewers ran against the merged post-fix corpus (`238de48`), per the docs/15
check stage: `review-simplicity` (changed-blocks scope), `review-conventions` (full corpus),
`review-functional` (full per-REQ trace vs the register @ `582bdeb`). All three ran blind to
`fix-wave-1.md`, the retrospective, and the validation notes. This document preserves the
verdicts and every actionable item; the full verbatim reports are in the session record.

## Verdicts

- **Functional: 45 implemented · 7 partial · 6 unimplemented · 10 disarmed · 0 contradicted ·
  1 out-of-scope** (pre-fix: 28/18/6/11/4/1). Every owner-ruled fix flipped to implemented with
  field-boundary evidence; all 14 spec-stated numbers verified into the correct timer PTs;
  anti-laundering clean; paradigm check clean.
- **Conventions (mechanical):** 16 findings (15E/1W), down from 17 — `FC_ControlMain`'s header
  finding cleared; both generated FBs and `FC_ControlMain` are now mechanically clean. All
  remaining in-block mechanical findings sit on imported-real content or the seven
  still-headerless generated files.
- **Simplicity:** the new code passes its own rules where it was careful (C-126, C-203, C-604's
  loud-gap idiom, named conditions in the sequencer) and was caught where it wasn't (below) —
  the reviewers flag our fixes exactly as designed.

## New defect candidates found by this pass (tier-1, owner verification wanted)

1. **Simultaneous jog buttons drive both solenoids** — `ExtendDemand`/`RetractDemand` jog terms
   have no cross-interlock; both held buttons assert `IO.Extend` and `IO.Retract` together
   (functional review). Candidate fix: mutual `NOT` terms on the jog paths.
2. **Power-cycle auto-resume, made concrete** — RETAIN `Step`/`RecentStart`/`RunFwd` + no OB100:
   a mid-run power cycle re-commands the motor on first scans, unwarned (functional +
   conventions C-124/C-403 cluster). This is Q-01 sharpened from question to traced behavior.
3. **`BothSwitchesFault` acts on nothing** — latched, alarmed (X3), but no transition/demand
   reads it; launching/jogging stay permitted with implausible limit switches (conventions).
4. **Fitted-drop mid-cycle strands `Step`** — all demands/timers/faults Fitted-killed, so a
   mid-stroke disable freezes the sequence state (cosmetic — no outputs — but `Step` parks
   non-zero; simplicity + functional both note it).
5. **Reversal-window comment contradicts the rung** — the comment describes a re-triggering
   window; the TON implements one fixed window from the first reversal. Logic satisfies
   REQ-028's text; the comment misleads (simplicity + functional; also C-202). Which semantics
   the plant NEEDS is a question, not a given.
6. **`EndTravelTimer` runs through pressure holds** — a long jam raises the timeout alarm on top
   of the jam (C-504 suppression candidate; may be deliberate escalation — verify intent).

## Findings on the fix wave's own new code (simplicity)

- **C-601 near-match reintroduced:** `LaunchRequest AND JogPreStartTimer.Q` duplicated between
  pusher N5 (with `OR IO.FaultReset` tail) and N7 — the exact divergence shape the rule exists
  for. Fix: name the fired-launch condition once.
- **C-604 rule-wording tension:** `FC_ControlMain` N3's three "deliberate constants" use the
  reserved `NOT AlwaysTrue` idiom with a justifying comment — the rule's commented-constant
  branch and its reservation clause genuinely conflict. Owner ruling on the rule text wanted;
  cleanest code fix regardless: named `...Fitted`-style proposed settings members.
- **C-605 still open at error severity:** UDT_PusherIO 30/32 bare members, UDT_ShredderSequencerIO
  23/28 — only the wave's NEW members got comments. A dedicated member-comment pass is due now
  that the toolchain supports it end-to-end (live-verified 2026-07-16).
- **C-603 partial:** two sequencer range predicates still lack stated insert-intent (the wave's
  own `PusherParkCmd` has it — extend the same comment to `MotorAutoStartCmd`/`MotorPreStartDoneCmd`/
  `RunDischargeConv`).
- Comment-vs-rung mismatches: `InfeedRunning` "Latched" (it isn't); reversal window (above).
- Constants-by-omission: `HandStartSignal` etc. unwired-but-undocumented next to the three
  strapped constants — one policy wanted.

## Conventions items (beyond the standing rework queue)

- **C-117 (error):** direction-change transitions (20→30, 60→30, 30→40) carry no
  not-running-feedback terms — protection rests on the imported FB's internal 8 s pause.
  Owner ruling: add `NOT ShredderRunFwdFB/RevFB` terms, or bless the FB-internal pause as
  satisfying C-117 for this integration (recorded either way).
- **C-121 letter question:** pusher N7's transitions guard `Step = 0` inside named request bits
  rather than inline — rule the named-bit form in or out.
- **C-123 ruling:** alarm-only `ParkedTimeoutFault` ("no safer step to force") vs the rule's
  explicit-recovery-transition demand.
- **C-103:** `FaultFB` set by FC wiring (plain coil, per scan) while the imported FB RCOILs it —
  cross-block S/R split; verify the FB's intended contract.
- **`RecentStart` two-writer handshake:** documented, scan-order-sound, but offends the one-writer
  principle's spirit — candidate rule gap; admit as idiom or restructure.
- **C-502 scale-down** (no FC_GeneralAlarms/FC_EStopAlarms; no unhealthy alarm at all),
  **C-503 minor** (X0 sourced from the raw buffer bit, not the UDT), **C-507** (X1–X8 are
  effectively ack-required without documented exceptions), **C-001 buffers** (Snake_Case renames
  queued), **C-115 applicability** to stepped architectures, vendor-default names
  (`Clock_0.5Hz`), C-001's `DO` vs `DQ` wording, the imported block's packed alarm word — all
  carried as owner items.
- **Converter bug report:** `review`'s status line said `C-301: checked, 2 finding(s)` for the
  imported FB but printed one finding block; SUMMARY matches the printed set. Small ticket vs
  the reporter/counter.

## Standing queue unchanged by this wave (already recorded)

Settings rework (C-307/C-308/C-122: delete the nine scan-copies, settings into UDTs with iDB
start values — the sub-struct design is live-verified and ready — **hold-off, owner 2026-07-17,
see `docs/notes/deferred-items.md` D-2**; the `DB_Settings` shrink-to-empty question is now
answered — see F-3 below), startup machinery (OB100 + DB_PLC + C-111 — **no longer waivable, see
below**), the overcurrent family (disarmed pending Q-11/Q-04 — Q-11 resolved 2026-07-17, still
disarmed for this prototype panel, see `requirements.md`; Q-04's setpoint numbers deferred, see
`docs/notes/deferred-items.md`), header-comment pass for the seven generated files, buffer
renames, InCycle member removal.

## F-3 — `DB_Settings` empty-end-state: resolved

Owner (round 3, 2026-07-17): "DB_Settings Can be empty. It probably wont be at the end of the day
but it can be." The settings rework (D-2, deferred) is not obligated to leave anything behind in
`DB_Settings` — zero members is a legitimate end state, not something to force content into to
avoid an empty DB. In practice it likely won't end up literally empty as new genuinely-plant-wide
settings get added over time, but that's a consequence of future need, not a design constraint.

## Owner verdicts on the B-docket (2026-07-17, `docs/notes/owner-questions.md` §B)

This is the accepted defect docket — validation corpus for `gen-block-modify-fix` once it exists
(owner-questions A-4). No code changed by this pass; rulings recorded so implementation can
proceed without re-litigating intent.

- **B-1 — Simultaneous jog buttons drive both solenoids. Confirmed fault. DONE (task 03,
  2026-07-17).** Owner: "This is a fault." Fix shape used: each jog demand term in
  `FB_PusherControl` NETWORK 11 now also excludes the opposite jog command in place
  (`ExtendDemand`'s jog term gained `AND NOT IO.JogRetractCmd`, `RetractDemand`'s gained
  `AND NOT IO.JogExtendCmd`) rather than naming a separate reused bit — each exclusion term is
  used exactly once, so C-601 doesn't call for a named intermediate. Holding both buttons now
  demands neither direction. Imported, block-compiled and whole-device-compiled clean (0 errors);
  untouched-network invariance confirmed (only NETWORK 11 differs from pre-edit HEAD across the
  whole file, including task 08's already-committed Network 10 draft, which survived the same
  round trip). Full IR diff, compile evidence, and the D-6 workaround finding: `agent-tasks/
  03-jog-interlock.md`'s exit and `docs/notes/deferred-items.md` D-6's update note.
- **B-2 — Power-cycle mid-run auto-resume. Confirmed fault. FIXED 2026-07-17.** Owner: "Needs
  addressed, is a fault." Ties to new doc-06 rule **C-128** and `requirements.md` Q-01 (both
  resolved this pass). Fix required the OB100/`DB_PLC` startup machinery this wave's fix left
  waived — the waiver is withdrawn (see below) and the machinery is now built, imported, and
  compiled clean (`agent-tasks/02-startup-machinery.md`; whole-device 0/0, both new blocks
  individually consistent, re-export readable-identical). C-111 full simulation-mode gating was
  deliberately left out of this pass (scoped down per the task doc's own permission — the traced
  auto-resume defect only needed the C-124/C-403/C-305 restart-safety half); flagged as a
  follow-up, not silently dropped.
- **B-3 — `BothSwitchesFault` acts on nothing. CORRECTED 2026-07-17: already satisfied, no fix
  needed.** Owner: "Should be assigned to an alarm bit in DB_Alarms for HMI to pick up." When this
  ruling was first recorded (round 1 of the owner-questions batch pass), it was summarized as "no
  HMI-visible alarm bit" — **that summary was wrong.** The original fix-wave-1 finding this traces
  to actually read "latched, alarmed **(X3)**, but no transition/demand reads it" — the `(X3)`
  citation means the alarm-word wiring already existed at finding time.
  `FC_AlarmsMain` Network 4 ("Pusher - Both Switches Active Simultaneously - Check Limit
  Switches") already writes `DB_Alarms.ShredderAlarm0.%X3 := iDB_PusherControl.IO.BothSwitchesFault`
  — grep-confirmed 2026-07-17, real sidecar present (already imported/compiled). The actual
  content of the finding — no transition or demand *reads* the fault, so it doesn't gate
  launch/jog/cycle — is exactly what the owner's ruling says is correct: annunciation-only,
  explicitly not an interlock. **There was nothing to build.** `agent-tasks/04-bothswitchesfault-
  alarm-wiring.md` closed as not-applicable; flagging the mis-transcription here rather than
  quietly dropping it, since it's the kind of error that could recur.
- **B-4 — `Fitted` dropped mid-cycle strands `Step`. Design ruling recorded.** Owner: "'Fitted'
  should really not change during machine operation, but if it is turned off all related
  processes should stop, if turned on again it shouldn't start unless called for." Reading:
  `Fitted` is an engineering-time setting, not meant for live toggling — but the logic must still
  handle a live drop defensively: an immediate `Fitted` clear stops every Fitted-gated process at
  once (not just freezes `Step`); re-enabling `Fitted` is never itself a start condition — a fresh
  normal call (cycle trigger) is required afterward, consistent with the new **C-128** no-auto-
  restart rule.
- **B-5 — Reversal-window semantics. FIXED 2026-07-17** (`agent-tasks/06-reversal-window-rearm-fix.md`).
  Owner: "May nor understand 100% but I can make a educated guess, you are looking to know if its
  ok to reuse the same reversal logic plus timer for overload events and yes that is ok." Reading:
  this confirms the REQ-028 "5 in 3 minutes" reversal-count mechanism (timer + counter) is a
  **single shared piece of logic used regardless of what triggers a reversal** (jam-clear reversal
  or an overcurrent-triggered reversal alike) — no separate counting family needed per cause.
  **The window-semantics question itself was resolved separately, round 3 of the owner-questions
  batch pass** ("it has to run clean for 180 second before reset" — re-arming, not fixed; see
  `requirements.md` REQ-028). Built: `FB_ShredderSequencer` Network 3's `TON` now reads
  `IN := NOT (IO.Step = 60 AND NOT ReversalStepEdgeMem) AND ReversalCount > 0` — IN drops false
  the scan a new reversal is detected (edge-tested against `ReversalStepEdgeMem`'s own
  previous-scan value, no cross-network staleness), resetting `ET`/`Q`, then re-arms. Compiled
  clean (whole-device 0/0, block + all flagged-inconsistent callers cleared); re-export diffed
  against pre-edit `HEAD` — only Network 3 (the `TON` line + new comment) and Network 4's comment
  changed, every other network byte-identical.
- **B-6 — `EndTravelTimer` runs through pressure holds. Ruling: add suppression.** Owner: "If
  another fault or expected event can explain why the pusher isn't fully extended, then interrupt
  that timer." Resolution: gate/hold `EndTravelTimer` during a recognized pressure-hold (REQ-046)
  or other named fault condition — C-504-style cause→consequence suppression, not a deliberate
  escalation. A long jam should raise the pressure/jam alarm alone, not stack a travel-timeout
  alarm on top.

**Startup-machinery waiver withdrawn (2026-07-17):** the demo-panel OB100/`DB_PLC` omission
(D-1's prior waiver) is superseded by B-2's confirmed-fault ruling and the new C-128 rule.
**Built same day** (`agent-tasks/02-startup-machinery.md`): `DB_PLC` (`NUMBER 7`, `Simulation :
Bool RETAIN = FALSE`) and `OB100` (`NUMBER 100`, `SECONDARYTYPE Startup`) imported and compiled
clean. Notable finding for the project's own grounding: this is the **first proven OB write-path
round trip** (`AITODO.md`'s "deliberately deferred" note previously said only OB1's *read* path
was proven) — `converter to-xml --synthesize` and `openness-cli import`/`compile` both handled a
hand-authored `SECONDARYTYPE Startup` OB correctly; TIA's own `Import()` silently corrected the
system-input parameter set from the (wrongly copied, OB1-shaped) `Initial_Call`/`Remanence` pair
to the real OB100 pair `LostRetentive`/`LostRTC`, and the committed IR now reflects that
re-exported truth. Also newly proven: the known "Inconsistent blocks... cannot be exported"
playbook entry (block-level `compile --block <name>`, callees before callers) applies to fresh
OB/DB creation, not just modification. C-111 full simulation-mode gating scoped out of this pass
(see B-2 above).

## C-11 — `RecentStart` two-writer handshake: marked as a temporary exception

Owner: "Mark as an exception, that will cleared later." The FC-latch / FB-self-clear two-writer
shape (documented, scan-order-sound per the original finding) is accepted as-is for now rather
than restructured or promoted to a general rule — **not** a precedent for other two-writer shapes.
It is expected to clear naturally alongside the `FC_ControlMain` N3 `AlwaysTrue`-constant cleanup
(the C-604 tension item) when that block of code is next touched — track together, don't fix one
without revisiting the other.

## F-1 — converter reporter bug: confirmed

Owner: "These should agree." The `review` status line's finding count and the printed finding
block must match; ticket the reporter/counter divergence as a real bug (small/non-blocking,
`docs/notes/owner-questions.md` F-1).

## Round 2 (2026-07-17): remaining rule-book items ruled

- **C-2 (C-117) — closed, no fix needed.** Owner: "No if it is interlocked in the FB there is no
  reason to duplicate elsewhere." The sequencer's direction-change transitions rely on
  `FB_MotorFwdRevSystem`'s own internal ~8s reversal pause; that satisfies C-117 for this
  integration (see doc 06 C-117's new clarification). No `NOT RunFwdFB/RevFB` terms added.
- **C-3 (C-121) — closed pending a grep-confirm.** Owner: reuse the named request bit in pusher
  N7 if it's a true equivalent of `Step = 0`. Doc 06 C-121 now states this explicitly. Before
  closing the specific N7 finding, confirm by grep that the named bit never reads true without
  `Step = 0` also true — if confirmed, no fix needed; if the bit is only an approximation, it
  still needs the inline form.
- **C-4 (C-123) — DONE (closed 2026-07-17, compiled clean via task 03's whole-file round trip).**
  Owner:
  "All faults must by reset by FaultReset." Doc 06 C-123 now states `FaultReset AND <fault> →
  step 0` satisfies the explicit-recovery-transition requirement even with no safer intermediate
  step. Phase 1 (grep-confirmed): no fault in `FB_PusherControl` forces a step-0 transition on
  `FaultReset` today — `ParkedTimeoutFault`/`BothSwitchesFault` are alarm-only latches cleared only
  by `NOT FaultReset` in their own coil, and `Blocked` merely gates `CycleRequest` re-entry (network
  1); none of the 7 `IO.Step` writes in the block are keyed off `FaultReset`. Network 10's own
  comment said so explicitly ("alarm-only by design, awaiting a human") — genuinely missing, not
  an oversight to just double-check.
  IR fix drafted: `MOVE(EN := IO.Step = 30 AND IO.ParkedTimeoutFault AND IO.FaultReset, IN := 0)
  => IO.Step` added to Network 10 (title/comment updated to match), touching only that network.
  **Blocked before Portal**: `converter preflight`/`to-xml` reject it —
  `IrFormatException: Network 10: IR has 2 move(s) but the sidecar records 1`. The converter has
  no supported path for "add one new statement to a network that already carries real sidecar
  data from a prior export" — only whole-file `--synthesize` for genuinely new, sidecar-less
  networks (which itself hard-errors if a real `SIDECAR` section is present). Per CLAUDE.md hard
  rule 7 this is a converter limitation to report, not a sidecar to hand-patch. Full writeup:
  `docs/notes/deferred-items.md` D-6 (workaround documented there — whole-file sidecar strip +
  `--synthesize`, proven by task 03).
  **Resolved without a converter fix, by the whole-file workaround:** task 03's own round trip on
  the same file (`FB_PusherControl.ir`) carried this Network 10 `MOVE` through to a clean compile
  as a side effect — re-exported IR confirmed byte-identical everywhere except task 03's own
  Network 11, meaning this fix survived untouched and is now live in the compiled scratch project.
  Verified 2026-07-17 (grep-confirmed against the current committed IR): Network 10 reads
  `MOVE(EN := IO.Step = 30 AND IO.ParkedTimeoutFault AND IO.FaultReset, IN := 0) => IO.Step`,
  exactly the drafted fix. No separate Portal round trip needed for this entry specifically.
- **C-5 (C-115) — closed, design change queued.** Owner: "They should expose that also, Always,
  just ignore when not needed." `FB_ShredderSequencer`'s and `FB_PusherControl`'s interface UDTs
  need `enable`/`ready`/`running`-equivalent members added (per doc 06 C-115's new clarification)
  even though this integration may not wire them yet. Queued as an interface-extension item
  alongside the B-docket work — an interface change, so it needs `gen-block-modify-purpose`
  (A-2/A-4) rather than `gen-block-modify-fix`.
- **C-7 (C-507) — closed, no rule or code change needed.** Owner's clarification (recorded
  verbatim in doc 06 C-507) resolves the apparent tension: GenProject1's latched,
  `FaultReset`-cleared X1–X8 alarm bits were never actually a C-507 exception case — PLC fault
  latching and HMI alarm acknowledgment are different mechanisms, and C-507's no-ack-required
  default stands correctly as originally written. The original finding's framing conflated the
  two; corrected here.
- **C-10 — recorded as tentative context, not a rule.** Owner: "I think its ok because it all
  writes to one word? but I dont know this is all vibes." Treated as the imported-real corpus
  already treats context findings: not a fix demand. If this FB is ever touched for other reasons,
  worth a real (non-vibes) check that a single Word write is in fact what C-501's word-packing
  scheme wants here.
- **F-2 (`FaultFB` cross-block contract) — closed, no fix needed.** Owner: "This is intend so
  that it may be lached/set outside of the block, and doesn't effect other use-cases." Doc 06
  C-103 now documents this as a named exception pattern. No restructuring needed.

**B-5 — RESOLVED (round 3, 2026-07-17): re-arming window, and this is a real logic fix, not a
comment fix.** Owner: "it has to run clean for 180 second before reset. Of course this timing has
to be adjustable." Reading, confirmed against the worked example: the count only clears after the
system runs **180 seconds with no new reversal** — every reversal restarts the 180s clock. Using
the example (reversals at t=0, 170, 340, 510s, each ~170s apart): under this semantics the system
**never gets a clean 180s gap, so the count never resets and trips at the 5th reversal**,
regardless of total elapsed time — this is the opposite of what fix wave 1 actually implemented
(one fixed 180s window measured from the first reversal only). **This promotes the finding from
"comment says one thing, rung says another" to a genuine functional defect**: the TON needs
restructuring to restart on every reversal (not just gate a fixed span from the first one), and
the count only clears on the timer's `Q` (180s clean). `DB_Settings.ReversalWindowTime` stays the
tunable (already adjustable, matches C-307) — no change needed there, just the retrigger logic.
Queued into the B-docket alongside B-1…B-6 as a `gen-block-modify-fix` candidate.
