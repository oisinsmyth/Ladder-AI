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
start values — the sub-struct design is live-verified and ready), startup machinery
(OB100 + DB_PLC + C-111, or a recorded waiver — now with the auto-resume evidence attached),
the overcurrent family (disarmed pending Q-11/Q-04), header-comment pass for the seven
generated files, buffer renames, InCycle member removal.
