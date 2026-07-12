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
`FB MotorDOL`, including the full telemetry network with its own OR-merge), WAND — bitwise word
AND (live-verified against `FB VSDUpdateComs`; closes out the AND-merge open item as **confirmed
non-existent** — no boolean parallel-branch AND-merge was found real anywhere in a 28-block sweep),
`Not` — standalone boolean inverter (implemented/tested/documented, **not yet TIA-cycle
live-verified** — see "Current task" below for why).

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 item 13 — `Not` — implementation + docs done, NOT YET COMMITTED

**Status as of 2026-07-12: fully implemented, tested (144/144 converter tests green), and now
documented (`ir/SPEC.md`, `src/converter/README.md`, `docs/notes/stage-gates.md`, `CHANGELOG.md`
all updated this pass). Waiting on explicit "commit this" from the project owner before
committing** — per this session's established discipline, never auto-commit.

**Grounded twice, independently** against real `FC PlantAutoControl` instances (UId=52, UId=72,
different networks, identical bare shape). Design: no new top-level production — `Not` is purely
a new chain-position kind, resolved via a recursive `TraceChain` call on its own `in` (same
pattern as an OR-merge branch), wrapped in the already-existing `Expr.Not`. Zero new IR-text
grammar, zero new `PartNode` fields, zero new parser/writer code (falls through to existing
generic bare-part handling both ways). Full design story: `docs/notes/stage-gates.md` ("S1 item
13") and `NotTests.cs`'s own class-level doc comment.

**Important, explicit gap — do not gloss over on resume:** `Not` is **not yet live-verified
against the true TIA gold standard** (`import → compile → re-export → Normalizer` cycle). Only
the in-memory pipeline (parse → reduce → build → write → reparse) is proven, against a
real-shaped fixture. A full sweep of `PlantAutoControl`'s 20 networks found **every one pairs `Not`
with a `<Call>`** (block calls, not yet built) — no real network can currently be isolated to
prove `Not` alone through the full cycle. This was a mid-session, project-owner-flagged
correction ("the gold standard is a lossless full cycle") — every prior "live-verified" claim in
this project (items 10–12: MOVE/OR-merge/WAND) was, on inspection, also only ever the in-memory
pipeline, not the true TIA cycle. This doc and `stage-gates.md` now say so explicitly rather than
implying more than is proven.

**Next task, explicitly decided by the project owner (`AskUserQuestion`): build block calls
next.** Chosen specifically because `Not` + `<Call>` co-occur in every real `PlantAutoControl`
network — building calls unlocks a genuine full-cycle proof for both together, closing the gap
above. This is expected to be a substantially bigger capability than `Not` was (FB instance
handling, multi-instance vs. global instance DB references, call-site argument binding — real
call targets already spotted: `MotorDOL`, `MotorVSDSystem`, `EquipmentControlSystem`, `AirStar`, `FilterUnitSystem`,
`ShredderControlSystem`, `TomraControlSystem`, `MotorFwdRevSystem`). **Not started at all yet** — no grounding, no
plan, no code. Approach with proper plan-mode rigor before writing anything, matching this
project's own established discipline for big new capabilities (see the OR-merge-generalization
plan file precedent).

Once `Not` is committed and block calls close (and later `SCoil`/`RCoil`, 3 each in
`PlantAutoControl`, still a separate deferred item), retest `PlantAutoControl` as a whole to see how much
further the real block progresses.
