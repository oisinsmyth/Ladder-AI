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
Confirmed closed so far within S1: walking skeleton (Contact/Coil), OR-merge + negated contacts,
Instance DB + structured members, DB round-trip, TON (both instance scopes, live-proven via
`FC TimerSample`), comparisons (Eq/Ge, live-verified against `FC ControlDelays`), MOVE (fan-out
taps + telescoping dedup, live-verified against `FB MotorDOL` — committed 2026-07-11).

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: none

S1 item 10 (MOVE) closed and committed. Next S1 item not yet chosen — check with the project
owner or `docs/notes/stage-gates.md`'s deferred-items list (multi-contact/nested OR-merge
branches, block calls, RCoil/SCoil, reference-by-name for structured members) for candidates.
