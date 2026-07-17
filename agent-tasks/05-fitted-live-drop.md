---
task: 05-fitted-live-drop
source: owner-questions B-4
portal: yes — GenProject1 scratch project (see agent-tasks/README.md queue before importing)
status: done (2026-07-17) — drafted by another concurrent session, verified compiled clean via task 07's round trip on the same file; see gen/GenProject1/fix-wave-1-reviews.md B-4
queue-order: 4
---

# Task 05 — Handle `Fitted` dropping mid-cycle

**Multi-agent note:** queue position 4. Wait for task 04 to show `done` in
`agent-tasks/README.md`'s Portal queue before claiming this slot.

## Background

Fix wave 1's check stage found: if `PusherFitted` (`DB_Settings.PusherFitted`, REQ-042) drops
mid-cycle, every Fitted-gated demand/timer/fault gets killed, but `Step` isn't force-parked — it
strands non-zero. No physical outputs result (cosmetic), but `Step` sits wrong and `Cycling` would
read true if anything ever consumed it.

The owner's design ruling (2026-07-17): *"'Fitted' should really not change during machine
operation, but if it is turned off all related processes should stop, if turned on again it
shouldn't start unless called for."* Read as: `Fitted` is meant to be an engineering-time setting,
not something toggled live — but the logic must still handle a live drop defensively. Two parts:

1. An immediate `Fitted` clear stops **every** Fitted-gated process at once — not just freezing
   `Step`, actually park/reset the pusher's run-state (force `Step` to 0/idle, per C-119).
2. Re-enabling `Fitted` is **never itself a start condition** — after re-enabling, the pusher must
   wait for a fresh, normal cycle-start trigger (REQ-034/035/036), same as any other idle state.
   This is consistent with the new **C-128** rule (no automatic restart after a stop event) —
   treat a `Fitted` drop-then-recover the same way as any other stop-then-restart.

Read before starting: `gen/GenProject1/fix-wave-1-reviews.md` B-4; `docs/06-lad-conventions.md`
C-119 (idle is always step 0), C-128 (no auto-restart); `gen/GenProject1/requirements.md`
REQ-042/043 (engineering enable/disable, no outputs/feedback-supervision when disabled).

## What to do

1. Grep `ir/GenProject1/FB_PusherControl.ir` for how `Fitted` currently gates logic — confirm the
   actual current shape before designing the fix.
2. Add a transition (or extend an existing fault/hold path) that force-writes `Step` to 0 the
   instant `Fitted` goes false while `Step <> 0` — this is a C-121-style `MOVE` transition, not a
   silent freeze.
3. Confirm (don't just assume) that no existing transition would let `Step = 0` auto-launch a
   cycle purely because `Fitted` just went true again — the normal cycle-start conditions
   (REQ-034/035/036: hopper-high in Auto, manual-cycle-cmd in Manual, etc.) must still be the only
   way in. If the current design already requires a fresh trigger by construction, say so in the
   presentation instead of adding redundant logic.

## Exit

Present: IR diff, one-paragraph intent, compile evidence, untouched-network invariance proof.
Update `gen/GenProject1/fix-wave-1-reviews.md`'s B-4 entry to mark it done. Release the Portal
queue slot in `agent-tasks/README.md`.
