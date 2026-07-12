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
— set/reset coils (committed `bf49afd`), network/block-level `Title` (committed `c41e891`),
`MUL`/`CONVERT` — arithmetic incl. ENO-chaining (implemented/tested/documented, **not yet
committed** — see "Current task" below). `FC PlantAutoControl` now converts as a whole block
(`to-ir → to-xml → to-ir` byte-identical) — first real production block this session to fully
round-trip. Full story for each: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 item 18 — `MUL`/`CONVERT` (arithmetic) — implementation + docs done, NOT YET COMMITTED

**Status as of 2026-07-12: fully implemented, tested (191/191 converter tests green), and now
documented (`ir/SPEC.md`, `src/converter/README.md`, `docs/notes/stage-gates.md`, `CHANGELOG.md`
all updated this pass). Waiting on explicit "commit this" from the project owner before
committing** — per this session's established discipline, never auto-commit.

**Picked up per the project owner's own explicit sequencing** at the close of S1 items 16/17
("Let's do block-level Title now, then scope arithmetic support" → "Commit this, then scope
arithmetic support. Plan mode please.") — the larger of the two remaining real gaps blocking
`PlantAutoControl`'s 8 dependency FBs (5 of 8 on `Mul`/`Convert`). Planned formally in plan mode
(`C:\Users\User\.claude\plans\quirky-gathering-ripple.md`).

**Phase 0 grounding found a genuine surprise, flagged before implementation, not forced to fit**:
real exports of `MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem` showed that despite
`DisabledENO="true"` on both `Mul` and `Convert` (matching the Move/WAND precedent that `eno` is
never wired), real networks chain `Mul`'s own `eno` output directly into the following
`Convert`'s own `en` input — a genuine control-flow dependency ("only run `Convert` if `Mul`
succeeded"), contradicting the plan's own inherited assumption. Flagged to the project owner via
a recommended design; approved: **"Go with that design."**

**Design**: new `EnSource` discriminated union (`Condition(Expr)` | `PrecedingEno`) rather than
folding the chain into `Expr` — there's no tag to reference "the preceding instruction's own
success" by. New reserved readable-form sentinel `EN := ENO`, mirroring the existing `TRUE`
sentinel precedent. `Mul`'s own type is `<AutomaticTyped Name="SrcType" />` (self-closing, no
value — TIA infers the type from the connected operands), modeled as `PartNode.AutomaticSrcType`.
`Convert` carries an ordinary `SrcType`/`DestType` `TemplateValue` pair. Full design story:
`docs/notes/stage-gates.md` ("S1 item 18") and `MulConvertTests.cs`'s own class-level doc comment.

**Live-verified against real data, 2026-07-12**: both the ENO-chained case (`MotorDOL`) and the
standalone rail-fed case (`ShredderControlSystem`) reduce and round-trip correctly. **Whole-block
`to-ir` sweep of all 5 previously-blocked dependency FBs**
(`MotorDOL`/`EquipmentControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem`/`ShredderControlSystem`) confirmed none hit a
`Mul`/`Convert` error anymore — all five now hit `TONR` instead, confirming it as **real** (was
"unconfirmed against any real data" as of the S1 item 15 docs).

Arithmetic beyond `Mul`/`Convert` (`Add`/`Sub`/`Div`/`Abs`/`Swap`/`Calc`) stays out of scope per
the plan's own explicit scoping — nothing observed needing it in any grounded network.

**Not yet raised with the project owner, pending this item's commit**: what to scope next —
`TONR` (now confirmed real, blocks all 5 of these dependency FBs) or the FC/FB parameter-interface
gap (`TomraControlSystem`/`MotorVSDSystem`/`AirStar`, 3 of 8) are the two live candidates. No decision made;
should be asked once this item is closed, not assumed.
