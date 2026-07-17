# Agent tasks — dispatch board

One file per task, written so you can point an agent at a single file and it has everything it
needs — no other context required. **`DISPATCH-TEMPLATE.md`** is a ready-to-paste prompt for
doing exactly that. This folder is the live dispatch board for queued work; it is
not a narrative log (that's `docs/notes/stage-gates.md`) and not the in-flight scratchpad for
whoever's actively working (that's `AITODO.md`). A task file moves here once its ruling/design
decision is settled and it's ready to hand to an agent; when done, its outcome gets folded back
into the permanent docs (`docs/06-lad-conventions.md`, `gen/GenProject1/*.md`,
`docs/notes/stage-gates.md`) and the task file itself gets deleted or marked done — don't let
finished tasks accumulate here.

**Every task file assumes multiple agents may be working on this project at the same time.**
Before touching anything, run `git status` and `git diff --stat` — don't trust that the repo is in
the state this doc describes; another agent's uncommitted or committed work may have changed it
since this file was written. If you find edits you don't recognize, stop and figure out whose they
are (check recent commits, check this README's queue table) before overwriting anything.

## The shared resource: GenProject1's Portal scratch project

CLAUDE.md's environment notes: two Openness/TIA Portal sessions on the **same** project
concurrently is unsupported — a genuine single-writer-file constraint, not a policy choice. Every
task below that ends in "import + compile against GenProject1" contends for that one resource.
Different projects are safe concurrently; this only matters for tasks sharing GenProject1.

**What's actually exclusive:** only the `openness-cli import` + `openness-cli compile` step.
Drafting IR changes, and running `converter preflight` (fully static, no Portal contact), is safe
to do **at any time, in parallel**, by any number of agents — including while another agent holds
the Portal slot below. Get your IR written and preflight-clean first; only queue for Portal once
you're actually ready to import.

### Portal queue protocol

1. Before running `openness-cli import`/`compile` against GenProject1, open this README and check
   the table below.
2. If any row shows `in-progress`, **do not open Portal.** Either wait, keep working your own
   task's IR-drafting/preflight phase, or work a `parallel-safe` task instead, and check back
   later.
3. If the row immediately above yours (lower **Order** number) is not yet `done`, wait for it —
   the order reflects a rough dependency/risk sequence (see each task's "Why this position" line),
   not an arbitrary queue number.
4. When clear to proceed: edit the table, set your row's **Status** to `in-progress` and
   **Claimed by** to something identifying you (session id, task description — anything that lets
   a human tell agents apart), save/commit that edit, *then* open Portal.
5. When your import+compile finishes (success, failure, or you're stopping for any reason), set
   **Status** to `done` or `blocked` (with a one-line reason) and clear **Claimed by**. This is
   what unblocks the next row — don't leave it `in-progress` after you've stopped working.
6. If you go to claim a row and find it's already `in-progress`, someone beat you to the edit —
   don't proceed, don't overwrite their claim. Tell the user.

This is a cooperative convention enforced by agents reading and editing a shared file, not a real
lock — it only works if every agent actually follows it. The user is the backstop: if Portal
sessions ever seem to be colliding, check `tasklist` for stray `Siemens.Automation.Portal.exe`
processes (CLAUDE.md's existing guidance) and check this table for a stale `in-progress` row.

### Queue

**Known blocker (2026-07-17, found by task 08):** the converter has no supported way to add a new
statement to a network that already has real sidecar data from a prior export (`converter
preflight`/`to-xml` throw `IrFormatException: Network N: IR has X move(s) but the sidecar records
Y`). Every row below except 02 modifies an already-exported network the same way task 08 did —
expect the identical error before you ever reach the Portal step. Full writeup:
`docs/notes/deferred-items.md` D-6; concrete case: `gen/GenProject1/fix-wave-1-reviews.md` C-4.
Check whether D-6 has been picked up before sinking time into drafting IR for 03/04/05/06/07/09 —
if it's still open, your IR draft + preflight will very likely hit the same wall task 08 did. **Update (task 03):** the whole-file strip-and-`--synthesize` workaround (fix-wave-1 precedent) still resolves this cleanly — confirmed via a full Portal round trip that also carried task 08's own Network 10 fix through to a clean compile. See `docs/notes/deferred-items.md` D-6's update note before assuming you're blocked.

| Order | Task | Status | Claimed by | Portal? |
|---|---|---|---|---|
| 1 | [`02-startup-machinery.md`](02-startup-machinery.md) | **done** | - | yes |
| 2 | [`03-jog-interlock.md`](03-jog-interlock.md) | done | - | yes |
| 3 | [`04-bothswitchesfault-alarm-wiring.md`](04-bothswitchesfault-alarm-wiring.md) | **done** (not applicable — already wired, task doc was based on a mis-transcribed finding) | - | yes |
| 4 | [`05-fitted-live-drop.md`](05-fitted-live-drop.md) | **done** (verified compiled clean via task 07's whole-file round trip on the same file; see `fix-wave-1-reviews.md` B-4) | - | yes |
| 5 | [`06-reversal-window-rearm-fix.md`](06-reversal-window-rearm-fix.md) | **done** | - | yes |
| 6 | [`07-endtraveltimer-suppression.md`](07-endtraveltimer-suppression.md) | **done** | - | yes |
| 7 | [`08-parkedtimeoutfault-recovery.md`](08-parkedtimeoutfault-recovery.md) | **done** (carried through by task 03's whole-file round trip; see `fix-wave-1-reviews.md` C-4) | - | conditional (see file) |
| 8 | [`09-sequencer-interface-extension.md`](09-sequencer-interface-extension.md) | ready (⚠ likely hits D-6) | - | yes |

**Parallel-safe (no Portal, no queue position — work anytime, alongside anything above):**

| Task | Status | Notes |
|---|---|---|
| [`01-pusher-n7-step-equivalence-check.md`](01-pusher-n7-step-equivalence-check.md) | ready | Pure grep/read; may feed into task 8 if it fails |
| [`discussion-a4-s7-ordering.md`](discussion-a4-s7-ordering.md) | ready | Conversation with the owner, not code |
| [`discussion-d4-stage-gates-review.md`](discussion-d4-stage-gates-review.md) | ready | Conversation with the owner, not code |

## Other shared resources, briefly

- **This README's queue table** — edited by every Portal-bound task at claim/release time; two
  agents editing it at the exact same moment could race. Low probability at human-paced dispatch;
  if you hit a merge conflict here, resolve by re-reading the current state, not by force-pushing
  your version.
- **`docs/06-lad-conventions.md`, `gen/GenProject1/requirements.md`,
  `gen/GenProject1/fix-wave-1-reviews.md`, `docs/notes/stage-gates.md`** — every task ends by
  folding its result back into one or more of these. If two Portal-queue tasks finish close
  together, their edits to these files should still land fine (different sections), but re-read
  the file immediately before editing it, don't assume it still matches what your task doc quoted.
- **Working tree / uncommitted changes** — this project does not mandate git worktrees for this
  kind of work, so by default every agent shares the same checkout. Commit your own change (or at
  minimum keep `git status` clean of anything but your own files) before considering a task done,
  so the next agent isn't looking at your half-finished edit.

## Task index (all tasks, by source ruling)

| Task file | Source | What |
|---|---|---|
| `01-pusher-n7-step-equivalence-check.md` | C-3 | Confirm a named bit is a true `Step = 0` equivalent |
| `02-startup-machinery.md` | B-2 / D-1 / Q-01 / C-128 | Build OB100 + `DB_PLC` + C-111 simulation gating |
| `03-jog-interlock.md` | B-1 | Mutual interlock on the pusher jog paths |
| `04-bothswitchesfault-alarm-wiring.md` | B-3 | Wire `BothSwitchesFault` into `DB_Alarms` |
| `05-fitted-live-drop.md` | B-4 | Handle `Fitted` dropping mid-cycle |
| `06-reversal-window-rearm-fix.md` | B-5 / REQ-028 | Fix the reversal-count window to re-arm |
| `07-endtraveltimer-suppression.md` | B-6 | Suppress `EndTravelTimer` during pressure holds |
| `08-parkedtimeoutfault-recovery.md` | C-4 | Verify/add `ParkedTimeoutFault`'s recovery transition |
| `09-sequencer-interface-extension.md` | C-5 | Add enable/ready/running to the two sequencer UDTs |
| `discussion-a4-s7-ordering.md` | A-4 | S7-first build-order conversation prep |
| `discussion-d4-stage-gates-review.md` | D-4 | stage-gates.md structure conversation prep |
