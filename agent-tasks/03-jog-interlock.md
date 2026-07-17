---
task: 03-jog-interlock
source: owner-questions B-1
portal: yes — GenProject1 scratch project (see agent-tasks/README.md queue before importing)
status: ready
queue-order: 2
---

# Task 03 — Mutual interlock on the pusher jog paths

**Multi-agent note:** queue position 2. Wait for `agent-tasks/02-startup-machinery.md` to show
`done` in `agent-tasks/README.md`'s Portal queue table before claiming this slot (that task
touches OB100/`DB_PLC`/Map FCs, not `FB_PusherControl`, so there's likely no file overlap — but
the queue exists to keep Portal import/compile serialized regardless, not just to avoid file
conflicts). You can draft this IR edit and run `converter preflight` any time before that.

## Background

Fix wave 1's check stage found `FB_PusherControl`'s jog terms (`ExtendDemand`/`RetractDemand` or
their current equivalent names — verify) have no cross-interlock: holding both jog buttons
simultaneously asserts both `IO.Extend` and `IO.Retract` at once. The owner confirmed this a
fault (2026-07-17): "This is a fault." Candidate fix already suggested in
`gen/GenProject1/fix-wave-1-reviews.md`'s B-1 entry: mutual `NOT` terms on the jog paths (each
demand excludes the other).

Read before starting: `gen/GenProject1/fix-wave-1-reviews.md` B-1; `gen/GenProject1/requirements.md`
REQ-038…041 (hand/jog requirements — jog is hold-to-move, REQ-039; independent of shredder,
REQ-040).

## What to do

1. Grep `ir/GenProject1/FB_PusherControl.ir` (confirm path) for the jog demand terms — find their
   actual current names, don't assume they still match the finding's original phrasing.
2. Add a mutual exclusion: `ExtendDemand` excludes `NOT RetractDemand-equivalent` (or the jog
   command bits directly, whichever is cleaner given the current logic shape) and symmetrically
   for retract. Keep it a **named condition** if it's reused (C-601) — don't duplicate the
   interlock term inline in two places if one named bit can serve both.
3. This is a **named-network** modification — touch only the jog networks, nothing else in the
   block. Follow CLAUDE.md's "Workflow for modifying existing logic": untouched-network invariance
   check, diff shows every changed network and proves the rest identical.

## Exit

Present: IR diff (should be small — one or two networks), one-paragraph intent, compile evidence,
untouched-network invariance proof. Update `gen/GenProject1/fix-wave-1-reviews.md`'s B-1 entry to
mark it done, with a one-line description of the actual fix shape used. Release the Portal queue
slot in `agent-tasks/README.md`.
