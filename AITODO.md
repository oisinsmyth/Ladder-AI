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
contacts, Instance DB + structured members, DB round-trip, TON/TONR/TOF (all instance scopes,
live-proven), comparisons (Eq/Ge/Lt/Ne), MOVE (fan-out taps + telescoping dedup), WAND (bitwise
word AND; closes out the AND-merge open item as **confirmed non-existent**), `Not` — standalone
boolean inverter, `CALL` — FB/FC block calls, `SCoil`/`RCoil` — set/reset coils, network/block-level
`Title`, `MUL`/`CONVERT`/`ADD` (arithmetic incl. ENO-chaining), FC/FB parameter-interface modeling
(`Input`/`Output`/`InOut`/`Constant`, committed `500dc68`), the `Mul`/`SrcType` fix (committed
`eaf93b0`), `Access Scope="LocalConstant"` (committed `55b5cb2`), `Ne` (not-equal comparison,
committed `70fbfbc`), `TOF` (off-delay timer) — **implemented/tested/documented, NOT YET
COMMITTED, see "Current task" below**. `FC PlantAutoControl` now converts as a whole block
(`to-ir → to-xml → to-ir` byte-identical) — first real production block this session to fully
round-trip; **6 of its 8 dependency FBs** now do too (`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/
`FilterUnitSystem`/`MotorFwdRevSystem` since S1 item 19; `AirStar` since this item). Only `TomraControlSystem`
(`Swap`) and `MotorVSDSystem` (a `<Call>` missing its `<Instance>`) remain blocked. Full story for each
closed item: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 item 23 — `TOF` (off-delay timer) — implementation + tests + docs DONE, NOT YET COMMITTED

**Status as of 2026-07-12: fully implemented, tested (230/230 converter tests green), live-verified
against real data (a genuine milestone — `AirStar` now fully round-trips), and documented
(`ir/SPEC.md`, `src/converter/README.md`, `docs/notes/stage-gates.md`, `CHANGELOG.md` all updated
this pass). Waiting on explicit "commit this" from the project owner before committing** — per
this session's established discipline, never auto-commit.

**Picked up per the project owner's own explicit choice** — asked directly what `TOF` was likely
to be (spotted blocking `AirStar` during S1 item 22's own live verification). Answered before any
grounding: an off-delay timer, IEC sibling of `TON`/`TONR`, both already fully supported.

**Grounded directly against real data** (real `FB AirStar`) rather than a fresh formal plan-mode
cycle — small, well-precedented (third `TimerKind` variant, after `TON`'s own original build and
`TONR`'s S1 item 19 addition). One retry needed on export (the recurring TIA-Portal-slow-wake
timeout, not a real error — succeeded on retry with a longer `--timeout-connect`). Found:
```xml
<Part Name="TOF" Version="1.0" UId="55">
  <Instance Scope="LocalVariable" UId="56">
    <Component Name="PulseProgramTimer" />
  </Instance>
  <TemplateValue Name="time_type" Type="Type">Time</TemplateValue>
</Part>
```
**Structurally identical to `TON`** — same `Version`/`Instance`/`time_type` shape, same
`IN`/`PT`/`ET` ports (confirmed by tracing the real wires within that specific network), **no
reset port** (unlike `TONR`), no `EN`/`ENO`. No live example of `TOF`'s own `Q` being consumed.
The only real difference from `TON` is semantic (off-delay vs on-delay timing), which this
converter doesn't compute anyway.

**Design**: `TimerKind` gains a third variant, `Tof` — needing **zero new fields**, simpler than
`TONR`'s own addition (which needed a real `Reset` field). Every touchpoint `TONR` already
generalized (`FlgNetParser.SupportedPartNames`/dispatch, `GraphReducer`'s `tonParts` filter/
`TimerKindFor`/`OutPortFor`/`TraceChain` upstream dispatch, `FlgNetBuilder.TimerPartNameFor`,
`IrSerializer`'s `TimerKeywordFor`/`TimerSidecarKind`, `IrParser`'s readable-form loop/regex/
sidecar `kind` parsing) just needed a third case each — plus one small extra guard in `IrParser`
rejecting a 4th (`R`-shaped) argument on a non-`TONR` timer line. `ReduceTimer`'s own
reset-resolution branch is already gated specifically on `Kind == TimerKind.Tonr`, so `TOF`
naturally skips it with no extra logic.

**Tests**: `TonTests.cs` — 5 new tests (mirroring the existing `WithTon.xml` coverage exactly:
parse/reduce/round-trip/serialize/full-block). New fixture `WithTof.xml`. All 230 converter tests
pass (up from 225).

**Live-verified against real data, 2026-07-12 — a genuine milestone**: exported `AirStar` fresh
and ran `converter to-ir` — **succeeded completely, no further errors at all.**
`to-ir → to-xml → to-ir` round-trips **byte-identical**, confirmed via `diff`. Confirmed this
genuinely exercises both `TOF` (1 occurrence) and `Ne` (2 occurrences) via direct grep, not a
lucky no-op. **`AirStar` is now the sixth of `PlantAutoControl`'s own 8 dependency FBs to fully
round-trip end to end** (S1 item 19 already got the other 5: `MotorDOL`/`EquipmentControlSystem`/
`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem`) — closing the entire chain of gaps this session's
own live-verification work progressively found for this specific block: S1 item 20's `Constant`
Interface section → S1 item 21's `LocalConstant` → S1 item 22's `Ne` → this item's `TOF`. All real
exported data deleted from scratch temp immediately after use, confirmed via `git status --short`.

**Note — a doc-accuracy correction made in this same pass**: earlier docs (S1 items 21/22)
described `AirStar` as becoming "the third" dependency FB to round-trip, assuming only
`MotorDOL`/`FilterUnitSystem` had succeeded before it. That undercounted — S1 item 19's own whole-block
sweep had already confirmed all 5 of `MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/
`MotorFwdRevSystem` round-tripping, not just the 2 that got the extra byte-identical double-check.
Fixed the "third" → "sixth" framing in `ir/SPEC.md`, `src/converter/README.md`, and
`docs/notes/stage-gates.md` as part of this item's own docs pass.

**Bottom line**: `TOF` is built, tested, and live-verified — the smallest of the three `TimerKind`
variants (no new fields at all) and the capstone of a four-item chain (S1 items 20–23) that
started with the FC/FB Interface modeling work. Only **2 of `PlantAutoControl`'s 8 dependency FBs
remain blocked**: `TomraControlSystem` (`Swap`) and `MotorVSDSystem` (a `<Call>` missing its own `<Instance>`).

**Not yet raised with the project owner, pending this item's commit — two live candidates now**:
1. `Swap` (blocks `TomraControlSystem`) — not grounded at the XML-shape level yet.
2. `<Call>` missing `<Instance>` (blocks `MotorVSDSystem`) — not grounded yet; every other `<Call>` seen
   so far (S1 item 14) has carried an `<Instance>`, so this is a genuinely new shape question, not
   just another "add a variant" pattern like the last four items.
Closing either would leave only one dependency FB blocked; closing both would mean **all 8** of
`PlantAutoControl`'s dependency FBs fully round-trip — worth flagging as the natural next milestone.
No decision made; should be asked once this item is closed, not assumed.

**Final verification before presenting for commit — still to do on resume if not already done**:
re-run all three test suites (`src/converter`/`src/openness-cli`/`tests/golden`) one final time to
reconfirm green, `git status --short` to confirm only expected files changed, then present for
explicit commit approval.
