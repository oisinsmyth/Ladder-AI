# AITODO — working state / recovery doc

**Purpose:** this project's context can get interrupted (session limits, restarts). This doc is
the recovery point — read it first after any gap, before trusting your own memory of "what I was
doing." Keep it up to date as you work: update the checklist as items close, don't wait for a
clean stopping point. `docs/notes/stage-gates.md` is the permanent, narrative record of *closed*
work; this doc is the scratchpad for *in-flight* work only. When a task here finishes and is
documented/committed, delete it from this file rather than letting it accumulate.

## Recovery procedure (do this first)

1. `git status` / `git diff --stat` in the repo root — uncommitted changes are the ground truth of
   in-flight work, more reliable than any narrative.
2. Read `docs/notes/stage-gates.md`'s tail (most recent entries) — the authoritative record of
   what's actually *closed*, with dates and evidence.
3. Read this file's "Current task" section below.
4. Cross-check: does the code in the diff match what this doc claims is done? If not, trust the
   code/diff and fix this doc.

## Project stage

**S1 — Lossless round-trip, ACTIVE.** See `docs/notes/stage-gates.md` for full gate history.
Confirmed closed so far within S1: walking skeleton (Contact/Coil), OR-merge (branches are
recursive chains — multi-Contact, nested, comparison-as-branch, all live-verified) + negated
contacts, Instance DB + structured members, DB round-trip, TON (both instance scopes, live-proven
via `FC TimerSample`), comparisons (Eq/Ge, live-verified against `FC ControlDelays`, including
`O(41)`-of-comparisons composition), MOVE (fan-out taps + telescoping dedup, live-verified against
`FB MotorDOL`, including the full telemetry network with its own OR-merge).

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: none

S1 item 11 (OR-merge branches generalized to recursive chains) fully done — code, tests (130/130
converter, 68 openness-cli, 11 golden-harness), docs, and live verification all complete and
consistent. This closed the exact loose end left open by both item 9 (`ControlDelays`) and item
10 (`MotorDOL`) — both real networks now reduce and round-trip completely, where both were
previously blocked at exactly this OR-merge limitation. Full story: `docs/notes/stage-gates.md`
"S1 item 11" + its live-verification section. Ready to commit as one unit (not yet committed —
commits are explicitly requested, not assumed).

Next S1 item not yet chosen. Remaining known deferred items (`docs/notes/stage-gates.md`'s "Open
items" sections, `ir/SPEC.md`'s "Open items"): AND-merge (`Part Name="A"`, unconfirmed), block
calls, RCoil/SCoil, TONR, full FC/FB parameter modeling, reference-by-name for structured members,
`Ne`/`Le`/`Gt`/`Lt` comparison operators (unconfirmed Part Names).
