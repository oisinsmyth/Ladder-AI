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
`Not` — standalone boolean inverter (committed `f6f7fa8`), `CALL` — FB/FC block calls (committed
`681fda2`; live-verified in-memory with `Not`, not yet TIA-cycle live-verified), `SCoil`/`RCoil`
— set/reset coils (committed `bf49afd`), network/block-level `Title` (implemented/tested/
documented, **not yet committed** — see "Current task" below). `FC PlantAutoControl` now converts as a
whole block (`to-ir → to-xml → to-ir` byte-identical) — first real production block this session to
fully round-trip. Full story for each: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 items 16/17 — network/block `Title` — implementation + docs done, NOT YET COMMITTED

**Status as of 2026-07-12: fully implemented, tested (177/177 converter tests green), and now
documented (`ir/SPEC.md`, `src/converter/README.md`, `docs/notes/stage-gates.md`, `CHANGELOG.md`
all updated this pass). Waiting on explicit "commit this" from the project owner before
committing** — per this session's established discipline, never auto-commit.

**A real design correction, not just a new field**: the IR's own `NETWORK <n> "<title>"` line
was actually sourced from `Comment`, not `Title` — invisible until `PlantAutoControl` (Title
populated, Comment empty everywhere, the opposite of every network grounded before it). Confirmed
the fix with the project owner first (`AskUserQuestion`): `NETWORK`'s own label now carries
`Title`; `Comment` gets its own new `COMMENT "..."` line. Block-level Title was a second,
unexpected finding — grounded while checking `PlantAutoControl`'s 8 dependency FBs (`MotorVSDSystem`/
`AirStar` both carry a real block-level Title, "VSD Motor") — same treatment, new `TITLE "..."`
line at the top of the file. Full design story: `docs/notes/stage-gates.md` ("S1 items 16/17")
and `NetworkTitleCommentTests.cs`'s own class-level doc comment.

**Live-verified against real data, 2026-07-12**: whole-block `PlantAutoControl` now converts
completely via `converter to-ir` — **the first real production block this whole session to fully
round-trip as a whole block** — `to-ir → to-xml → to-ir` byte-identical. Block-level Title
confirmed via `MotorVSDSystem`/`AirStar` (both now progress past Title to a different, already-known
gap — FC/FB parameter `Interface` sections).

**Attempted the true TIA cycle per the project owner's explicit instruction: export from
`JOB9002`, import into `SampleProject` (not `JOB9002`), run the cycle there.** Import succeeded
(TIA accepted the regenerated XML into an unrelated project); **compile failed with 502 errors**
— entirely missing tags (323 distinct paths) and missing FB library blocks (8 dependency FBs) —
`SampleProject` has neither `PlantAutoControl`'s tag table nor its FB library. **Not a converter
defect** — environmental/dependency limitation, a block doesn't carry its project context with
it. Left the imported-but-uncompiled `PlantAutoControl` block in `SampleProject` — project owner will
clean it up manually (confirmed in advance; `openness-cli` has no delete/remove-block command).

**Grounded the 8 dependency FBs directly** (project owner's own follow-up): **0 of 8 convert
cleanly today.** `Mul`/`Convert` (arithmetic) blocks 5; FC/FB parameter `Interface` sections
(`Input`/`Constant`) block the other 3 (`TomraControlSystem`, and now `MotorVSDSystem`/`AirStar` too, having
cleared the Title check).

**Next task, per the project owner's own explicit decision: scope arithmetic support
(`Add`/`Sub`/`Mul`/`Div`/`Convert`/etc.) next** — the larger of the two remaining real gaps by
block count (5 of 8 dependency FBs). **Not started at all** — no grounding, no design, no code.
Given the size (same "boxed instruction family" shape as WAND, but a whole family rather than one
instruction), likely warrants proper plan-mode rigor, matching this project's own discipline for
"substantially bigger" capabilities (the `CALL` precedent).

Full FC/FB parameter-interface modeling (`Input`/`Output`/`InOut`/`Constant` sections) remains a
separate, larger, already-known deferred item — not in scope for the arithmetic work, blocks
`TomraControlSystem`/`MotorVSDSystem`/`AirStar` regardless of arithmetic support landing.
