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
`Title`, `MUL`/`CONVERT`/`ADD` (arithmetic incl. ENO-chaining, committed `ce0be15`/`77f0207`), FC/FB
parameter-interface modeling (`Input`/`Output`/`InOut`/`Constant`) — **implemented/tested/
documented, NOT YET COMMITTED, see "Current task" below**. `FC PlantAutoControl` now converts as a
whole block (`to-ir → to-xml → to-ir` byte-identical) — first real production block this session to
fully round-trip; `MotorDOL`/`FilterUnitSystem` (2 of its 8 dependency FBs) now do too. Full story for
each closed item: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 item 20 — FC/FB parameter-interface modeling — implementation + docs + live verification DONE, NOT YET COMMITTED

**Status as of 2026-07-12: fully implemented, tested (211/211 converter tests green), live-verified
against real data, and documented (`ir/SPEC.md`, `src/converter/README.md`,
`docs/notes/stage-gates.md`, `CHANGELOG.md` all updated this pass). Waiting on explicit "commit
this" from the project owner before committing** — per this session's established discipline,
never auto-commit.

**Picked up per the project owner's own explicit request** ("let's approach the interface side of
the FC/FBs") — the last real gap left from the original 8-FB `PlantAutoControl` dependency sweep.
Planned formally in plan mode (`C:\Users\User\.claude\plans\quirky-gathering-ripple.md` — full
authoritative design record, not duplicated in full here).

**Phase 0 grounding carried the strongest mandate of any item this session**: zero real
`Input`/`Output`/`InOut`/`Constant` member XML had ever been captured anywhere in the codebase
before this item. Exported `TomraControlSystem`/`MotorVSDSystem`/`AirStar` fresh (scratch temp, deleted
immediately after use, twice — confirmed via `git status --short`). Found:
- `Input`/`Output`: same shape as `Static`'s own full member shape, but genuinely missing the
  `SetPoint` `BooleanAttribute` `Static` always carries — `DbInterfaceMembers.ParseMember`/
  `WriteMember` gained a `requireSetPoint`/`includeSetPoint` parameter (default `true`, `Static`'s
  own proven behavior unchanged) rather than a parallel type.
- `Constant`: a genuinely distinct third shape (`Name`/`Datatype`/`Accessibility="Public"` +
  required `StartValue`, no `AttributeList`/`Remanence` at all) — new
  `ParseConstantMember`/`WriteConstantMember`.
- `InOut`: present-but-always-empty in all 3 grounded FBs — no populated example anywhere.
- `Return`: absent entirely on all 3 FBs (confirmed FC-specific) — found and fixed a related bug:
  `BlockSourceWriter` was emitting the `Ret_Val` boilerplate unconditionally for every block;
  fixed to only emit it for non-FB blocks.

**Design**: `BlockSource`/`IrBlock` gained `InputMembers`/`OutputMembers`/`ConstantMembers`
(nullable, mirrors `StaticMembers`) and `InOutMembers` (never null, mirrors `TempMembers`).
Deliberately a block-level concept only, per ADR-0001 (`CALL`'s own call-site wiring, S1 item 14,
untouched). Sanitization extended: `Input`/`Output`/`InOut`/`Constant` member names sanitized the
same way `Static`/`Temp`'s already are (freely block-owner-chosen, not structurally exempted).
Also corrected `ir/SPEC.md`'s own stale `INTERFACE` grammar sketch, which had never listed
`STATIC` despite it being the one section actually implemented since S1 item 7 Phase B.

**File-by-file status (all done)**: `SimaticMl/DbInterfaceMembers.cs` (SetPoint-optional
Input/Output + new Constant shape), `SimaticMl/Model.cs` + `Ir/Model.cs` (new fields on
`BlockSource`/`IrBlock`), `SimaticMl/BlockSourceParser.cs`/`BlockSourceWriter.cs` (parse/write all
4 sections + the Return fix), `Ir/IrSerializer.cs`/`IrParser.cs` (readable-form `INPUT`/`OUTPUT`/
`INOUT`/`CONSTANT` subsections), `Program.cs` (threaded new fields through `ConvertToIr`/
`ConvertToXml`), `Sanitize/Sanitizer.cs` (sanitizes the 4 new member lists).

**Tests**: `BlockInterfaceTests.cs` — 5 new tests plus `Parse_FbWithNonEmptyInput_HardErrors`
repurposed into a positive test (`Parse_FbWithInputOutput_ReadsBothAsMemberLists`) — its own old
fixture was synthetic, never sourced from a real export, corrected to the real grounded shape at
the same time. New fixture `FbWithConstant.xml`. All 211 converter tests pass (up from 207).

**Live-verified against real data, 2026-07-12**: exported all 3 previously-Interface-blocked FBs
fresh (`TomraControlSystem`/`MotorVSDSystem`/`AirStar`) and ran `converter to-ir` on each — **the Interface
gap is genuinely closed for all three**, confirmed via distinct error messages showing each now
fails for an unrelated reason:
- `TomraControlSystem` hits `Swap` (unsupported instruction, not grounded).
- `MotorVSDSystem` hits `Access Scope="LocalConstant"` (unconfirmed Access scope, not grounded).
- `AirStar` hits a **real, confirmed correction needed to already-committed S1 item 18 code**:
  `Mul UId=43` carries an ordinary `<TemplateValue Name="SrcType" Type="Type">Real</TemplateValue>`
  instead of the self-closing `<AutomaticTyped Name="SrcType" />` shape S1 item 18 confirmed
  universal from `MotorDOL`/`EquipmentControlSystem`. `FlgNetParser` hard-errors on this today rather than
  silently guessing — flagged as a new open item, **not fixed as part of this item** (would need
  its own grounding pass: is `AutomaticTyped` vs. explicit `SrcType` a real TIA-exposed choice, or
  does it correlate with something about how the operands are wired?).

All real exported data deleted from scratch temp immediately after use, confirmed via
`git status --short`.

**Bottom line**: the Interface modeling gap itself is fully closed and live-proven. None of the 3
target FBs fully round-trips as a whole block yet — each hits one further, unrelated, newly-found
gap. The most consequential of the three: **the `Mul`/`SrcType` finding directly affects
already-shipped code from S1 items 18/19**, worth the project owner's attention before any further
arithmetic work, independent of whatever gets scoped next.

**Not yet raised with the project owner, pending this item's commit — three live candidates now**:
1. The `Mul`/`SrcType` correction (touches already-committed code — arguably the most urgent).
2. `Swap` (blocks `TomraControlSystem`'s own full round-trip).
3. `Access Scope="LocalConstant"` (blocks `MotorVSDSystem`'s own full round-trip).
No decision made; should be asked once this item is closed, not assumed.

**Final verification before presenting for commit — still to do on resume if not already done**:
re-run all three test suites (`src/converter`/`src/openness-cli`/`tests/golden`) one final time to
reconfirm green, `git status --short` to confirm only expected files changed, then present for
explicit commit approval.
