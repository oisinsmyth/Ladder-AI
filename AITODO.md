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
contacts, Instance DB + structured members, DB round-trip, TON/TONR (both instance scopes,
live-proven), comparisons (Eq/Ge/Lt), MOVE (fan-out taps + telescoping dedup), WAND (bitwise word
AND; closes out the AND-merge open item as **confirmed non-existent**), `Not` — standalone
boolean inverter, `CALL` — FB/FC block calls, `SCoil`/`RCoil` — set/reset coils, network/block-level
`Title`, `MUL`/`CONVERT`/`ADD` (arithmetic incl. ENO-chaining), FC/FB parameter-interface modeling
(`Input`/`Output`/`InOut`/`Constant`, committed `500dc68`). `FC PlantAutoControl` now converts as a
whole block (`to-ir → to-xml → to-ir` byte-identical) — first real production block this session to
fully round-trip; `MotorDOL`/`FilterUnitSystem` (2 of its 8 dependency FBs) now do too. `Mul`'s own
`SrcType` fix is **implemented/tested, NOT YET COMMITTED — see "Current task" below**. Full story
for each closed item: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: `Mul`'s own `SrcType` fix — implementation + tests + docs DONE, NOT YET COMMITTED

**Status as of 2026-07-12: fully implemented, tested (216/216 converter tests green), live-verified
against real data, and documented (`ir/SPEC.md`, `src/converter/README.md`,
`docs/notes/stage-gates.md`, `CHANGELOG.md` all updated this pass). Waiting on explicit "commit
this" from the project owner before committing** — per this session's established discipline,
never auto-commit.

**Picked up per the project owner's own explicit instruction** ("Commit this, then let's fix the
Mul/SrcType issue") — a correction to already-committed S1 item 18 code, found during S1 item 20's
own live verification (`FB AirStar`'s own `Mul UId=43` carries an ordinary `<TemplateValue
Name="SrcType" Type="Type">Real</TemplateValue>` instead of the self-closing `<AutomaticTyped
Name="SrcType" />` shape S1 item 18 confirmed universal from `MotorDOL`/`EquipmentControlSystem`). Already
grounded (one confirmed real instance, captured directly during S1 item 20's live verification and
already recorded in `docs/notes/stage-gates.md`'s own S1 item 20 section) — implemented directly
without a fresh formal plan-mode cycle, since the shape was small, contained, and already real-data
confirmed.

**Design**: `FlgNetParser.ParseMulFixedShape` now accepts either shape (hard-errors only if both
or neither present, never guesses which is "the real one"). No new `PartNode` field needed — the
*existing* `AutomaticSrcType: bool`/`SrcType: string?` fields already coexist generically on that
record (used independently by other Part kinds). `MulStatementSidecar` gained a nullable `SrcType`
field (sidecar-only, mirroring `Convert`'s own precedent — readable IR text unaffected).
`FlgNetWriter` needed **zero changes** — its existing writing branches were already generic enough.

**File-by-file status (all done)**: `SimaticMl/FlgNetParser.cs` (`ParseMulFixedShape` returns
`(Cardinality, AutomaticSrcType, SrcType)`, dispatch updated), `Ir/Model.cs`
(`MulStatementSidecar.SrcType`), `GraphReducer.cs` (`ReduceMul` captures/validates the shape),
`SimaticMl/FlgNetBuilder.cs` (`BuildMul` reconstructs whichever shape), `Ir/IrSerializer.cs`/
`IrParser.cs` (sidecar `srctype = ...` line, optional).

**Tests**: `MulConvertTests.cs` — 5 new tests (parse/reduce/round-trip/full-block for the
explicit-`SrcType` shape, plus a negative "both present" test). New fixture
`MulWithExplicitSrcType.xml` (standalone rail-fed `Mul`, genericized — real `AirStar` wiring
wasn't captured in detail during the brief live-verification grep that found this, so the
fixture's own wiring mirrors the already-proven standalone-rail-fed pattern, not a literal claim
about `AirStar`'s exact wire UIds). All 216 converter tests pass (up from 211).

**Live-verified against real data, 2026-07-12**: exported `AirStar` fresh and ran `converter
to-ir` — **the `Mul`-specific error is gone**. The block now progresses to a different,
already-known, unrelated gap: `Access Scope="LocalConstant"` (the same one `MotorVSDSystem` — same FB
family, both titled "VSD Motor" — already hits). All real exported data deleted from scratch temp
immediately after use, confirmed via `git status --short`.

**Bottom line**: the `Mul`/`SrcType` counter-example is fixed and live-proven against the real
block that surfaced it. Two further, unrelated open items remain from S1 item 20's own
live-verification pass — `Swap` (blocks `TomraControlSystem`) and `Access Scope="LocalConstant"` (blocks
`MotorVSDSystem`/`AirStar` both) — neither addressed here.

**Not yet raised with the project owner, pending this fix's commit — two live candidates now**:
1. `Swap` (blocks `TomraControlSystem`'s own full round-trip) — not grounded at the XML-shape level yet.
2. `Access Scope="LocalConstant"` (blocks `MotorVSDSystem`'s AND `AirStar`'s own full round-trip, larger
   impact by block count) — not grounded at the XML-shape level yet.
No decision made; should be asked once this fix is closed, not assumed.

**Final verification before presenting for commit — still to do on resume if not already done**:
re-run all three test suites (`src/converter`/`src/openness-cli`/`tests/golden`) one final time to
reconfirm green, `git status --short` to confirm only expected files changed, then present for
explicit commit approval.
