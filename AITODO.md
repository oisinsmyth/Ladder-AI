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
boolean inverter, `CALL` — FB/FC block calls (incl. FC calls with no `<Instance>`), `SCoil`/
`RCoil` — set/reset coils, network/block-level `Title`, `MUL`/`CONVERT`/`ADD` (arithmetic incl.
ENO-chaining), FC/FB parameter-interface modeling (`Input`/`Output`/`InOut`/`Constant`, committed
`500dc68`), the `Mul`/`SrcType` fix (committed `eaf93b0`), `Access Scope="LocalConstant"`
(committed `55b5cb2`), `Ne` (not-equal comparison, committed `70fbfbc`), `TOF` (off-delay timer,
committed `3f66880`), `CALL` without `<Instance>` (a real FC call) — **implemented/tested/
documented/live-verified, NOT YET COMMITTED, see "Current task" below**. `FC PlantAutoControl` now
converts as a whole block (`to-ir → to-xml → to-ir` byte-identical) — first real production block
this session to fully round-trip; **7 of its 8 dependency FBs** now do too (`MotorDOL`/
`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem` since S1 item 19; `AirStar` since S1
item 23; `MotorVSDSystem` since this item). Only `TomraControlSystem` (`Swap`) remains blocked. Full story for
each closed item: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 item 24 — `CALL` without `<Instance>` (a real FC call) — implementation + tests + docs + live verification DONE, NOT YET COMMITTED

**Status as of 2026-07-12: fully implemented, tested (236/236 converter tests green, up from 230),
live-verified against real data (a genuine milestone — `MotorVSDSystem` now fully round-trips), and
documented (`ir/SPEC.md`, `src/converter/README.md`, `docs/notes/stage-gates.md`, `CHANGELOG.md`
all updated this pass). Waiting on explicit "commit this" from the project owner before
committing** — per this session's established discipline, never auto-commit.

**Picked up per the project owner's own explicit choice** ("Commit this, then let's do MotorVSDSystem's
Call/Instance gap"), to close `MotorVSDSystem`'s own hard error:
`SimaticMlFormatException: <Call UId="52"> is missing its <Instance> element.` Every `<Call>`
grounded so far (S1 item 14, 20 real instances) called an FB and carried an `<Instance>` — a
genuinely new shape question, not just another "add a variant" pattern. Went through a full formal
plan (`EnterPlanMode`/`ExitPlanMode`, plan file
`C:\Users\User\.claude\plans\quirky-gathering-ripple.md`) with mandatory Phase 0 grounding before
any code, given the bigger scope: changing already-shipped `CallStatement`/`CallStatementSidecar`
record fields from non-nullable to nullable.

**Phase 0 grounded against real `MotorVSDSystem`** (scratch temp, deleted after use) — the working
hypothesis (a stateless FC call) confirmed exactly:
```xml
<Call UId="52">
  <CallInfo Name="Scale" BlockType="FC">
    <Parameter Name="Input" Section="Input" Type="Real" />
    <Parameter Name="Input_Min" Section="Input" Type="Real" />
    <Parameter Name="Input_Max" Section="Input" Type="Real" />
    <Parameter Name="Scaled_Min" Section="Input" Type="Real" />
    <Parameter Name="Scaled_Max" Section="Input" Type="Real" />
    <Parameter Name="Output" Section="Output" Type="Real" />
  </CallInfo>
</Call>
```
`BlockType="FC"` — a call to Siemens' own standard-library `Scale` function, stateless by design.
**No `<Instance>` element at all**, genuinely absent. Parameters (5 Input + 1 Output, all `Real`)
fit the existing allowlist unchanged. `en` rail-fed, same as every other real Call. Only one
`<Call>` in `MotorVSDSystem` — no mixed FB/FC scenario exercised by this specific instance, though the
design supports both generically.

**Design**: `SimaticMl.Model.PartNode.Instance` was already `AccessNode?` (no change needed).
`Ir.Model.CallStatement.InstancePath` (`string` → `string?`) and `CallStatementSidecar`'s
`InstanceUId`/`InstanceScope`/`InstanceComponentPath` (all three → nullable, one all-or-nothing
group, not a discriminated union — no second "kind" to distinguish, just presence/absence).
`FlgNetParser.ParseCall` checks `<Instance>`'s presence directly (not gated on `BlockType`, since
nothing rules out a real counter-example either way even though the two correlate).
`FlgNetWriter.WriteCall` mirrors this symmetrically. `GraphReducer.ReduceCall`'s own doc comment
("Instance is required... every Call carries one") was corrected; `FlgNetBuilder.BuildCall`'s own
unconditional `AccessNode` construction became conditional (this was a real, expected compile
error in the interim state, caught and fixed as part of the same pass). Readable-form grammar
omits the instance argument entirely when absent (`CALL Scale(EN := ..., ...)`), disambiguated on
parse by checking whether the first split argument itself starts with the reserved `EN := `
prefix (no real instance path could ever collide with it) — mirrors `EnSource`'s own `ENO`
sentinel and `TimerBinding`'s optional `R :=` argument. The sidecar's `instanceuid =`/
`instancescope =`/`instancepath =` lines are omitted together when absent, mirroring the timer
sidecar's own optional `reset` lines (S1 item 19).

**Tests**: `CallTests.cs` — 6 new tests (parse/reduce/sidecar/round-trip/serialize/full-block,
mirroring the existing with-Instance coverage exactly for the no-Instance case). New fixture
`CallFcNoInstanceFedByRail.xml` (genericized down from the real 5+1 shape to 2+1, mirroring
`CallWithParametersFedByRail`'s own genericization precedent). All 236 converter tests pass (up
from 230). `openness-cli` (68) and golden-harness (11) suites unaffected, confirmed still green.

**Live-verified against real data, 2026-07-12** — fresh `MotorVSDSystem` export from the real
`JOB9002_PLC` device (one retry needed on the export connect — the recurring TIA-Portal-slow-wake
timeout, not a real error): `converter to-ir` **succeeded completely, no errors at all.**
`to-ir → to-xml → to-ir` round-trips **byte-identical**, confirmed via `diff` between the
first-pass and round-tripped `.ir`. Confirmed genuinely exercising this item's own work via direct
grep of the converted `.ir` text: `CALL Scale(EN := TRUE, Input := IO.SpeedPerc, Input_Min := 0.0,
Input_Max := 100.0, Scaled_Min := 0.0, Scaled_Max := IO.MaxRPM, Output => IO.SpeedOutput)` — no
instance argument, exactly the confirmed shape. All real exported data deleted from scratch temp
immediately after use, confirmed via `git status --short`.

**`MotorVSDSystem` is now the seventh of `PlantAutoControl`'s own 8 dependency FBs to fully round-trip end to
end.** Only `TomraControlSystem` (`Swap`) remains blocked — the last gap standing between here and all 8
dependency FBs round-tripping.

**Not yet raised with the project owner, pending this item's commit — one live candidate now**:
`Swap` (blocks `TomraControlSystem`) — not grounded at the XML-shape level yet. Closing it would mean
**all 8** of `PlantAutoControl`'s dependency FBs fully round-trip — worth flagging as the natural next
milestone once this item is closed, not assumed.

**Final verification before presenting for commit — already done this pass**: all three test
suites re-run and confirmed green (236 converter / 68 openness-cli / 11 golden-harness),
`git status --short` confirmed only expected files changed (no real restricted data). Ready to
present for explicit commit approval.
