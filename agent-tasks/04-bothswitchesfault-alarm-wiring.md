---
task: 04-bothswitchesfault-alarm-wiring
source: owner-questions B-3
portal: yes — GenProject1 scratch project (see agent-tasks/README.md queue before importing)
status: ready
queue-order: 3
---

# Task 04 — Wire `BothSwitchesFault` into `DB_Alarms`

**Multi-agent note:** queue position 3. Wait for task 03 to show `done` in
`agent-tasks/README.md`'s Portal queue before claiming this slot.

## Background

`BothSwitchesFault` (both pusher limit switches on at the same time — implausible, REQ-052) is
latched and set, but fix wave 1's check stage found it wired to nothing else — no HMI-visible
alarm bit, and it doesn't gate launch/jog/cycle. The owner ruled (2026-07-17): "Should be assigned
to an alarm bit in DB_Alarms for HMI to pick up." Explicitly **annunciation-only** — the owner did
not ask for it to become an interlock. Don't add gating logic beyond what's asked (C-606).

Read before starting: `gen/GenProject1/fix-wave-1-reviews.md` B-3; `gen/GenProject1/requirements.md`
REQ-052 (source text, alarm class); `docs/06-lad-conventions.md` C-501 (alarm word packing —
`<Category>Alarm0`, `<Category>Alarm1`… slice-access exception, one alarm bit per network with
the network title stating the alarm text).

## What to do

1. Find `DB_Alarms`'s current category word structure (grep `ir/GenProject1/DB_Alarms.ir` or
   equivalent) and the appropriate category/next free bit for a pusher fault.
2. In `FC_AlarmsMain` (or wherever alarm-monitoring FCs live per C-109/C-502 — verify current
   structure), add one network: title states the alarm text (C-505 format:
   `<Equipment> — <fault> — <action hint>`), body writes `BothSwitchesFault` into the chosen
   `DB_Alarms` slice-access bit (C-501's documented C-301 exception — exactly one alarm bit per
   network).
3. Do not add any interlock/gating logic — this is annunciation only, per the owner's explicit
   scope.

## Exit

Present: IR diff (should be one new network, maybe a `DB_Alarms` member addition), one-paragraph
intent, compile evidence, untouched-network invariance proof for anything else in the touched
blocks. Update `gen/GenProject1/fix-wave-1-reviews.md`'s B-3 entry to mark it done. Release the
Portal queue slot in `agent-tasks/README.md`.
