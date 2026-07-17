---
task: 01-pusher-n7-step-equivalence-check
source: owner-questions C-3 (doc 06 C-121)
portal: no — pure grep/read, no Portal contact, no compile gate
status: done (2026-07-17) — confirmed genuine equivalents, no fix needed; see gen/GenProject1/fix-wave-1-reviews.md C-3
---

# Task 01 — Confirm pusher N7's named bit is a true `Step = 0` equivalent

**Multi-agent note:** this task is parallel-safe — it never touches Portal and never edits
GenProject1's IR, so it can run at any time alongside anything else in this folder's queue. It
only reads files. Still run `git status` first in case another agent has an uncommitted edit to
the file you're about to read (unlikely to matter for a read-only task, but check).

## Background

`docs/06-lad-conventions.md` C-121 requires a stepped sequence's transitions to be a plain Int
comparison gating a `MOVE`: `EN := Step = <from> AND <condition>`. A finding on
`FB_PusherControl` network 7 (or wherever it currently lives — see below) showed a transition
guarding `Step = 0` **inside a named request bit** rather than writing the comparison inline.

The owner ruled (2026-07-17, recorded in C-121's text): a named bit that is a **genuine
equivalent** of `Step = <from>` — it contains that comparison and is never true without it also
being true — satisfies the rule, and reusing it is actually preferred (C-601's name-it-once
principle). An **approximate** or conditionally-narrower bit does not qualify and still needs the
inline form. This task is the "confirm it's a true equivalent" step that was left open — see
`gen/GenProject1/fix-wave-1-reviews.md`'s "Round 2" section, C-3 entry, and
`docs/06-lad-conventions.md` C-121 for the full rule text.

## What to do

1. Read `gen/GenProject1/requirements.md` and `ir/GenProject1/FB_PusherControl.ir` (confirm the
   actual current filename/path first — `git status`/`ls ir/GenProject1/` — the corpus may have
   moved since this task was written).
2. Find the pusher network that transitions out of `Step = 0` via a named request bit (originally
   flagged as "network 7" — network numbering may have shifted since; search by content, not by
   assumed number).
3. Trace the named bit's own definition. Confirm, by reading the logic (not by assuming): does
   the bit read true in **every** case where `Step = 0 AND <the transition's other conditions>`
   would be true, and **never** true otherwise? If yes, it's a genuine equivalent. If the bit is
   ANDed/ORed with anything else that makes it narrower or broader than the literal
   `Step = 0 AND <condition>` expression, it is **not** a qualifying equivalent.
4. Record the finding — no code change either way, this is a verification task:
   - **If confirmed equivalent:** note in `gen/GenProject1/fix-wave-1-reviews.md`'s C-3 entry that
     the grep-confirm passed, cite the bit's name and where it's defined, and close the item —
     no fix needed.
   - **If NOT a true equivalent:** this becomes a real C-121 finding needing a fix (write
     `Step = 0` inline, or fix the bit to be a genuine equivalent). Add it to
     `agent-tasks/README.md`'s Portal queue as a new numbered task (append after task 8, or ask
     the user where to slot it), and note the discrepancy in `fix-wave-1-reviews.md`.

## Exit

Update `gen/GenProject1/fix-wave-1-reviews.md`'s C-3 entry with the confirmed outcome (cite the
actual bit name and file/network you found, not the placeholder "N7" from the original finding).
No compile gate applies to this task — it's read-only. Report back what you found.
