---
task: 07-endtraveltimer-suppression
source: owner-questions B-6
portal: yes — GenProject1 scratch project (see agent-tasks/README.md queue before importing)
status: ready
queue-order: 6
---

# Task 07 — Suppress `EndTravelTimer` during pressure holds

**Multi-agent note:** queue position 6. Wait for task 06 to show `done` in
`agent-tasks/README.md`'s Portal queue before claiming this slot.

## Background

`EndTravelTimer` (pusher taking too long to reach the end-travel switch, REQ-054) currently runs
through pressure holds (REQ-046: pusher stops and holds position if the high-pressure switch
activates >0.5s while pushing) — so a long jam raises the travel-timeout alarm on top of the
pressure-hold condition, double-alarming one root cause. The owner ruled (2026-07-17): "If another
fault or expected event can explain why the pusher isn't fully extended, then interrupt that
timer." This is a C-504 cause→consequence suppression, not a deliberate escalation.

Read before starting: `gen/GenProject1/fix-wave-1-reviews.md` B-6; `docs/06-lad-conventions.md`
C-504 (one alarm rung monitors one fault; a consequence alarm is suppressed by `-|/|-` on the
causal condition, extended by a short TON so slow-recovering equipment doesn't flash consequence
alarms while the cause resets); `gen/GenProject1/requirements.md` REQ-046/054.

## What to do

1. Grep `ir/GenProject1/FB_PusherControl.ir` for `EndTravelTimer`'s current `IN` gating and for
   the pressure-hold condition/state (REQ-046's implementation — likely a `Hold` bit per C-123,
   not a `Step` change, since holds don't write `Step`).
2. Add a `-|/|-` on the pressure-hold condition to `EndTravelTimer`'s `IN`, per C-504's pattern —
   extended by a short TON if the hold can clear and re-trigger quickly enough that a bare `-|/|-`
   would still let a flash-alarm through (use judgment; state your reasoning in the network
   comment either way).
3. Confirm this doesn't change REQ-046/047's own behavior (resume push after pressure clears +
   5s delay) — you're only touching what feeds the *timeout alarm's* timer, not the pressure-hold
   logic itself.

## Exit

Present: IR diff, one-paragraph intent, compile evidence, untouched-network invariance proof.
Update `gen/GenProject1/fix-wave-1-reviews.md`'s B-6 entry to mark it done. Release the Portal
queue slot in `agent-tasks/README.md`.
