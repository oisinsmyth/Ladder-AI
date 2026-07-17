---
task: 06-reversal-window-rearm-fix
source: owner-questions B-5 (REQ-028)
portal: yes — GenProject1 scratch project (see agent-tasks/README.md queue before importing)
status: done (2026-07-17) — see gen/GenProject1/fix-wave-1-reviews.md B-5 and requirements.md REQ-028 for the built shape
queue-order: 5
---

# Task 06 — Fix REQ-028's reversal-count window to re-arm (genuine functional defect)

**Multi-agent note:** queue position 5. Wait for task 05 to show `done` in
`agent-tasks/README.md`'s Portal queue before claiming this slot. **This is the highest-stakes
task in the queue** — it's a confirmed functional defect (not a comment/cosmetic fix), so be
extra careful with the compile-gate and invariance proof.

## Background

REQ-028: "If 5 overcurrent events occur within 3 minutes, the system stops and an alarm is
raised." Fix wave 1 implemented this as **one fixed 180s window measured from the first
reversal** — count resets when that window elapses, regardless of how the reversals inside it
were spaced. The owner corrected this (2026-07-17): *"it has to run clean for 180 second before
reset. Of course this timing has to be adjustable."*

**The correct semantics: re-arming, not fixed.** The count only clears after the plant runs
**180 seconds with no new reversal** — every reversal restarts the 180s clock. Worked example: 
reversals at t=0, 170, 340, 510s (each ~170s apart, always under 180s from the previous one) —
under the correct semantics the system **never gets a clean 180s gap, so the count never resets,
and it trips at the 5th reversal** however long that takes in total. This is the opposite of what
a fixed-window-from-first-event implementation would do (which would reset the count partway
through and never trip in this example).

Read before starting: `gen/GenProject1/requirements.md` REQ-028 (has the full resolved-semantics
note); `gen/GenProject1/fix-wave-1-reviews.md` B-5 entry (has the same, plus the fix-wave-1
implementation this replaces).

## What to do

1. Grep `ir/GenProject1/FB_ShredderSequencer.ir` (confirm path) for the current reversal-count
   logic — the TON gated by `DB_Settings.ReversalWindowTime` (180.0) and the counter compared
   against `DB_Settings.ReversalCountThreshold` (5).
2. Restructure so the timer **restarts on every new reversal event** (retriggerable — since
   doc 06 C-406 forbids TP/TOF/TONR, this needs constructing a restart from TON + edge logic per
   C-404, same pattern as any other explicit-construction timer in this codebase) rather than
   running once from the first event. The count clears when the timer reaches `Q` (180s with no
   new reversal), not on a fixed elapsed-from-first-event basis.
3. `DB_Settings.ReversalWindowTime` stays the tunable span — no change needed to the setting
   itself, only to how it's used (re-arm behavior).
4. Fix the network comment too — the original finding was partly that the comment claimed
   re-arming behavior the old rung didn't implement (C-608, new rule: "a comment must not
   contradict its rung"). Once the rung matches the comment, this resolves itself, but double
   check the wording is still accurate to the new logic shape.

## Exit

Present: IR diff, one-paragraph intent explicitly stating the semantics change (fixed → re-arming)
and citing the owner ruling, compile evidence, untouched-network invariance proof. This one
deserves a clear before/after description in the presentation given it changes actual trip
behavior, not just readability. Update `gen/GenProject1/fix-wave-1-reviews.md`'s B-5 entry to mark
it done. Release the Portal queue slot in `agent-tasks/README.md`.
