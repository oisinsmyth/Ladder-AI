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
`Not` — standalone boolean inverter (committed `f6f7fa8`), `CALL` — FB/FC block calls
(implemented/tested/documented, **not yet TIA-cycle live-verified** — see "Current task" below).

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 item 14 — `CALL` — implementation + docs done, NOT YET COMMITTED

**Status as of 2026-07-12: fully implemented, tested (158/158 converter tests green), and now
documented (`ir/SPEC.md`, `src/converter/README.md`, `docs/notes/stage-gates.md`, `CHANGELOG.md`
all updated this pass). Waiting on explicit "commit this" from the project owner before
committing** — per this session's established discipline, never auto-commit.

**Grounded first (Phase 0), per plan-mode approval, before any code.** Real `<Call>` shape:
genuinely not a `<Part Name="Call">` — its own sibling element under `<Parts>`
(`<Call UId="N"><CallInfo Name="..." BlockType="FB"><Instance/><Parameter/>...</CallInfo></Call>`).
19 of 20 real instances have zero wired parameters (not present-but-empty, simply absent); one
real instance (`CompileUnit "35"`, "Tomra Auto Control") has 10 (8 Input, 2 Output). Instance
reuses the exact same `AccessNode` shape as TON's own `<Instance>`. Design: new top-level
production (like TON/Move/WAND) — `en` via the same `TraceChain` fan-out mechanism, arguments
resolved by iterating the source's own sparse `<Parameter>` list. Full design story:
`docs/notes/stage-gates.md` ("S1 item 14") and `CallTests.cs`'s own class-level doc comment.

**Live-verified in-memory, 2026-07-12:** isolated `CompileUnit "35"` — the richest real network
found, combining `Not` (wrapping an OR-of-comparisons) with the fully-wired 10-parameter `Call`,
8 independent Contact→Coil rungs, and more. **Reduces and round-trips completely** — the first
real network proving `Not`+`Call` together through the in-memory pipeline.

**Important, explicit gap — do not gloss over on resume:** still **not verified through the true
TIA `import → compile → re-export → Normalizer` cycle**. `openness-cli import` has no
per-network granularity (whole-block only), and `PlantAutoControl` as a whole still has `SCoil`/
`RCoil` elsewhere (3 each) — so a whole-block `RoundTripRunner.RunFull` would still fail there,
unrelated to `Call` itself. A splice-and-reimport approach (regenerate just this one network's
XML, splice it into the otherwise-untouched real export, reimport the whole file) was identified
as technically feasible but **not attempted** — it means writing regenerated content back into
the real (scratch-copy) `PlantAutoControl` block, even if scoped to one network, so it was left for
the project owner's own explicit call rather than assumed.

**Next task, per the project owner's own explicit sequencing: `SCoil`/`RCoil` (set/reset
coils).** 3 each in `PlantAutoControl`, real, seen alongside TON/MOVE in earlier grounding, still out
of scope. This is the most natural path to finally reaching the true TIA-cycle proof for a whole
real block — once built, `PlantAutoControl` should have no remaining unsupported constructs and a
whole-block `RunFull` becomes possible without any special splicing. **Not started at all yet** —
no grounding, no plan, no code.

Once `CALL` is committed and `SCoil`/`RCoil` close, retest `PlantAutoControl` as a whole — it should be
the first real production block to fully round-trip, and the point to finally attempt the true
TIA-cycle live verification.
