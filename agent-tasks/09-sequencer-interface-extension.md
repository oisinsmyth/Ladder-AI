---
task: 09-sequencer-interface-extension
source: owner-questions C-5 (doc 06 C-115)
portal: yes — GenProject1 scratch project (see agent-tasks/README.md queue before importing)
status: ready
queue-order: 8
---

# Task 09 — Add enable/ready/running vocabulary to the sequencer UDTs

**Multi-agent note:** queue position 8 — **last** in the Portal queue, deliberately. This is an
**interface change** (bigger and more invasive than the other tasks, which are logic-only fixes
inside existing interfaces) — doing it last means it lands after the other fixes are already
compiled and stable, reducing the chance of this touching a network another queued task also
needed to touch. Wait for task 08 to show `done` before claiming this slot.

**This task also needs an architecture-level check, not just a code fix** — see "Gate" below.

## Background

`docs/06-lad-conventions.md` C-115 requires every equipment FB to expose the same handshake
vocabulary (`enable` in; `ready`, `running` out) through its interface UDT. `FB_ShredderSequencer`
and `FB_PusherControl` — both consciously-stepped sequences (C-113 "yes") — don't expose it; they
expose `Step`/`Mode` instead. This was flagged as a pattern-vs-rule tension and the owner ruled
(2026-07-17): *"They should expose that also, Always, just ignore when not needed."* Now doc 06
C-115's own text. Applies to **every** equipment FB, stepped or chained-permissive — a caller may
leave the members unwired if this integration has no use for them, but the interface UDT carries
them regardless.

Read before starting: `docs/06-lad-conventions.md` C-115 (full resolved text), C-118/C-125 (`Step`
and its faults already live in the interface UDT — this is the same UDT, add members alongside);
`gen/GenProject1/fix-wave-1-reviews.md` C-5 entry.

## Gate — this is an interface change, treat it as one

Per `docs/15-generation-pipeline.md`'s hard gate 1: "before any block is coded, whenever the
request creates new blocks or **interfaces**." Adding members to `UDT_PusherIO` and
`UDT_ShredderSequencerIO` is exactly that. **Before writing IR:** produce at minimum a
mini-manifest (per `gen-architecture`'s "Scale-down: the mini-manifest" — touched blocks,
interface changes: yes, exactly which members, tag status) and treat it as needing the engineer's
go-ahead before the code-and-compile phase, not just a diff at the end. If no engineer is
available to gate-check synchronously, draft the mini-manifest, present it clearly separated from
the rest of the presentation, and say explicitly that this part is unreviewed pending sign-off.

## What to do

1. Grep both UDTs (`ir/GenProject1/UDT_PusherIO.ir`, `ir/GenProject1/UDT_ShredderSequencerIO.ir`
   or equivalent — confirm current names) for their current member lists.
2. Add `enable`/`ready`/`running`-equivalent members (name them consistently with whatever the
   site's chained-permissive equipment pattern actually calls them — check `patterns/` for the
   real member names per C-115's own instruction to take vocabulary from the admitted pattern, not
   doc 06's illustrative names).
3. Leave them **unwired** by the current callers (`FC_ControlMain`) unless there's an obvious,
   low-risk place to wire them meaningfully (e.g. `running := Step <> 0` is a defensible default
   for `running`, similar to the pusher's own `Cycling` idiom) — don't invent caller-side usage
   beyond what's asked.
4. This touches the UDT files and, if you add member comments, satisfies part of the standing
   C-605 queue too (bonus, not required scope — don't go looking for other bare members to fix
   here).

## Exit

Present: the mini-manifest (Gate, above) first; then IR diff, one-paragraph intent, compile
evidence, untouched-network invariance proof for every block whose call sites reference these
UDTs. Update `gen/GenProject1/fix-wave-1-reviews.md`'s C-5 entry to mark it done. Release the
Portal queue slot in `agent-tasks/README.md`. This is the last item in the current queue — note
in the README that the Portal queue is empty/complete once this lands.
