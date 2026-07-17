---
task: 08-parkedtimeoutfault-recovery
source: owner-questions C-4 (doc 06 C-123)
portal: conditional — verify phase is Portal-free; fix phase (if needed) uses GenProject1 scratch project
status: done (2026-07-17) — fix carried through to a clean compile by task 03's whole-file round trip; see gen/GenProject1/fix-wave-1-reviews.md C-4
queue-order: 7
---

# Task 08 — Verify/add `ParkedTimeoutFault`'s recovery transition

**Multi-agent note:** queue position 7 **only applies to the fix phase**. The verify phase (step
1 below) is pure grep/read — parallel-safe, do it anytime, no need to wait for task 07. Only
claim the Portal queue slot in `agent-tasks/README.md` if the verify phase finds a real gap
requiring a fix.

## Background

`ParkedTimeoutFault` (pusher taking too long to reach parked, REQ-055) was originally flagged as
alarm-only with no recovery transition — C-123 requires every fault to be "handled by an explicit
transition to a defined recovery/abort step — never a silent freeze," and the original finding
noted there's "no safer step to force" the pusher into from a parked-timeout state.

The owner's ruling (2026-07-17, now doc 06 C-123's own text): *"All faults must by reset by
FaultReset."* This is universal — **`FaultReset AND <fault> → step 0` (idle) is itself the
recovery transition** when no safer intermediate step applies. A fault is never left with no
transition at all; the transition may simply be "back to idle," but it must exist and be wired.

Read before starting: `docs/06-lad-conventions.md` C-123 (has the full resolved text);
`gen/GenProject1/fix-wave-1-reviews.md` C-4 entry; `gen/GenProject1/requirements.md` REQ-055,
REQ-050 (pusher locked out until fault reset — general precedent for the `FaultReset` pattern this
should match).

## What to do

**Phase 1 — verify (no Portal):**

1. Grep `ir/GenProject1/FB_PusherControl.ir` for `ParkedTimeoutFault` and for how other faults in
   the same block wire their `FaultReset → step 0` transition (e.g. `PressureTripCount`'s
   `BothSwitchesFault`/blocked-fault handling, or whatever pattern the block already uses — find
   the actual precedent, don't invent one).
2. Confirm whether `ParkedTimeoutFault` already has an equivalent `FaultReset AND
   ParkedTimeoutFault → Step := 0` transition wired somewhere, or whether it's genuinely missing.

**Phase 2 — fix, only if Phase 1 found a real gap (needs Portal):**

3. Add the missing transition, matching the block's existing `FaultReset`-handling pattern for
   consistency (C-607 — one problem, one policy per block).
4. Named-network modification only — touch just what's needed for this transition, invariance
   check on the rest.

## Exit

If Phase 1 finds the transition already exists: update `gen/GenProject1/fix-wave-1-reviews.md`'s
C-4 entry to mark it closed, no fix needed, and cite where the existing transition lives. No
compile gate applies. If Phase 2 was needed: present IR diff, intent, compile evidence, invariance
proof; update the C-4 entry to mark it done; release the Portal queue slot in
`agent-tasks/README.md`.
