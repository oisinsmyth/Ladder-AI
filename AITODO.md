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
(`Input`/`Output`/`InOut`/`Constant`, committed `500dc68`), the `Mul`/`SrcType` fix (committed
`eaf93b0`), `Access Scope="LocalConstant"` — **implemented/tested/documented, NOT YET COMMITTED,
see "Current task" below**. `FC PlantAutoControl` now converts as a whole block (`to-ir → to-xml →
to-ir` byte-identical) — first real production block this session to fully round-trip;
`MotorDOL`/`FilterUnitSystem` (2 of its 8 dependency FBs) now do too. Full story for each closed item:
`docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 item 21 — `Access Scope="LocalConstant"` — implementation + tests + docs DONE, NOT YET COMMITTED

**Status as of 2026-07-12: fully implemented, tested (221/221 converter tests green), live-verified
against real data, and documented (`ir/SPEC.md`, `src/converter/README.md`,
`docs/notes/stage-gates.md`, `CHANGELOG.md` all updated this pass). Waiting on explicit "commit
this" from the project owner before committing** — per this session's established discipline,
never auto-commit.

**Picked up per the project owner's own explicit choice** — the last of the two real gaps S1 item
20's own live verification found (`Swap`, blocking only `TomraControlSystem`, stays deferred;
`LocalConstant` blocks both `MotorVSDSystem`/`AirStar`). Planned formally in plan mode
(`C:\Users\User\.claude\plans\quirky-gathering-ripple.md` — full authoritative design record).

**Phase 0 grounding (done, real data deleted)**: exported `MotorVSDSystem`/`AirStar` fresh from `JOB9002`
(scratch temp, deleted immediately after use, confirmed via `git status --short`). Found a genuine
fourth Access shape across 4 independent instances (`MotorVSDSystem`: `MinSpd` ×2; `AirStar`:
`PulseTimerMS` ×2):
```xml
<Access Scope="LocalConstant" UId="22">
  <Constant Name="MinSpd" />
</Access>
```
A bare, self-closing reference by name — **no value at all present at the reference site** —
neither `AccessNode`'s own `<Symbol>` shape nor `ConstantAccessNode`'s `<ConstantType>`/
`<ConstantValue>` shape. `MinSpd`/`PulseTimerMS` are exactly the real member names S1 item 20's
own grounding already confirmed as populated `Constant`-section members on these same two blocks
— this is how a network reads back a reference to the block's own declared Interface `Constant`
member. Always single-component; always at a `ResolveTagOrLiteralOperand`-style operand position
(TON `PT`, comparison operands, `Move`'s own `in`) — never a plain Contact/Coil operand.

**Design**: modeled as an `AccessNode` with a one-element `ComponentPath`, reusing
`DottedPath`/`FromDottedPath` completely unmodified (confirmed by inspection: a single-component
path already round-trips through both with zero changes). The IR's own tag-ref text (e.g.
`PulseTimerMS`) reads identically to the member's own declared name in that block's own
`INTERFACE`/`CONSTANT` section (S1 item 20) — a real correlation, not just a convenient encoding.
The one unavoidable cost: `FlgNetParser.ParseAccess`/`FlgNetWriter`'s `AccessNode`-writing loop
both needed a scope-conditional branch. `GraphReducer.cs` needed **zero changes** —
`ResolveTagOrLiteralOperand` dispatches by UId lookup, not scope.

**File-by-file status (all done)**: `SimaticMl/FlgNetParser.cs` (`SupportedAccessScopes` gains
`"LocalConstant"`; `ParseAccess` branches to new `ParseLocalConstantAccess`), `SimaticMl/
FlgNetWriter.cs` (`AccessNode`-writing loop gains a scope-conditional early branch).

**Tests**: `TonTests.cs` — 5 new tests (a TON `PT` fed by `LocalConstant`, matching `AirStar`'s
own real structural position: parse/reduce/round-trip incl. explicit XML-shape assertion/
serialize, plus a negative "unexpected content" test). New fixture
`WithTonPtFedByLocalConstant.xml`. All 221 converter tests pass (up from 216).

**Live-verified against real data, 2026-07-12**: exported `MotorVSDSystem`/`AirStar` fresh and ran
`converter to-ir` on each — **the `LocalConstant` error is gone from both**. Neither fully
round-trips as a whole block yet — each hits a different, new, unrelated gap:
- `MotorVSDSystem` hits `<Call UId="52">` missing its own `<Instance>` element (not yet grounded — every
  `<Call>` seen so far, S1 item 14, has carried one).
- `AirStar` hits `Ne` (not-equal) — an unsupported comparison Part Name, the first confirmed-real
  sibling of `Eq`/`Ge`/`Lt` (the IEC family's own `Ne`/`Le`/`Gt`, previously all unconfirmed).

All real exported data deleted from scratch temp immediately after use, confirmed via
`git status --short`.

**Bottom line**: `Access Scope="LocalConstant"` is built, tested, and live-verified — the last of
the two real gaps S1 item 20's own live verification surfaced is now closed. Neither `MotorVSDSystem`
nor `AirStar` fully round-trips yet (never guaranteed to by this item), each now blocked by a
separate, unrelated, newly-found item — neither addressed here.

**Not yet raised with the project owner, pending this item's commit — three live candidates now**:
1. `Swap` (blocks `TomraControlSystem`) — not grounded at the XML-shape level yet.
2. `<Call>` missing `<Instance>` (blocks `MotorVSDSystem`) — not grounded yet.
3. `Ne` (blocks `AirStar`) — likely a cheap extension of the already-generic comparison machinery
   (same family as `Eq`/`Ge`/`Lt`), similar in spirit to how `Lt` itself was added in S1 item 19.
No decision made; should be asked once this item is closed, not assumed.

**Final verification before presenting for commit — still to do on resume if not already done**:
re-run all three test suites (`src/converter`/`src/openness-cli`/`tests/golden`) one final time to
reconfirm green, `git status --short` to confirm only expected files changed, then present for
explicit commit approval.
