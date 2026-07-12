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
— set/reset coils (implemented/tested/documented, **not yet committed** — see "Current task"
below). Full story for each: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 item 15 — `SCoil`/`RCoil` — implementation + docs done, NOT YET COMMITTED

**Status as of 2026-07-12: fully implemented, tested (164/164 converter tests green), and now
documented (`ir/SPEC.md`, `src/converter/README.md`, `docs/notes/stage-gates.md`, `CHANGELOG.md`
all updated this pass). Waiting on explicit "commit this" from the project owner before
committing** — per this session's established discipline, never auto-commit.

**Grounded first (two independent real instances of each), before any code.** Real shape:
completely bare `<Part Name="SCoil"/"RCoil" UId="N" />`, exact same `in`/`operand` wire shape as
a plain `Coil`, never a producer — structurally identical to `Coil` in every respect. Smallest
diff of any S1 item this session: `GraphReducer.ReduceOneChain`/`FlgNetBuilder.BuildOneChain`
reused verbatim for all three kinds, tagged with a new `CoilAssignment.Kind` field
(`Assign`/`Set`/`Reset`) — no new sidecar field needed, since `BuildOneChain` already takes the
model alongside its sidecar. Readable-form keywords `SCOIL`/`RCOIL` (matching every other
keyword's mirror-the-source-Part-Name convention). Full design story: `docs/notes/stage-gates.md`
("S1 item 15") and `SCoilRCoilTests.cs`'s own class-level doc comment.

**Live-verified in-memory, 2026-07-12:** isolated the real network already grounded for `CALL`
(it also has the block's own `SCoil`/`RCoil` pair) — reduces and round-trips completely.

**Then attempted a whole-block round-trip of `PlantAutoControl`** — since `SCoil`/`RCoil` was believed
to be its last instruction-level gap, this seemed like the moment to finally reach the true
TIA-cycle proof. **Hit a different, already-known wall instead**: every one of `PlantAutoControl`'s 20
real networks carries a non-empty network `Title` (distinct from `Comment`), which
`BlockSourceParser` already hard-errors on (documented earlier during the reference-project
corpus work, `tests/golden/README.md` — not a new discovery, just newly encountered on this real
block). `PlantAutoControl` has no remaining *instruction-level* gap but still doesn't round-trip as a
whole block, for a reason entirely unrelated to any instruction type.

**Next steps, not yet decided — ask the project owner:** whether to build network `Title`
modeling next (this would very plausibly be the thing that finally gets `PlantAutoControl` to a true
whole-block TIA-cycle proof), or something else. **Not started at all** — no design, no code.
