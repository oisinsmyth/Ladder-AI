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
`RCoil` — set/reset coils, network/block-level `Title`, `MUL`/`CONVERT`/`ADD`/`SWAP` (arithmetic
incl. ENO-chaining), FC/FB parameter-interface modeling (`Input`/`Output`/`InOut`/`Constant`,
committed `500dc68`), the `Mul`/`SrcType` fix (committed `eaf93b0`), `Access
Scope="LocalConstant"` (committed `55b5cb2`), `Ne` (not-equal comparison, committed `70fbfbc`),
`TOF` (off-delay timer, committed `3f66880`), `CALL` without `<Instance>` (a real FC call,
committed `32e5228`), `SWAP` (byte-swap box instruction) — **implemented/tested/documented/
live-verified, NOT YET COMMITTED, see "Current task" below**. `FC PlantAutoControl` now converts as a
whole block (`to-ir → to-xml → to-ir` byte-identical) — first real production block this session to
fully round-trip; **all 8 of its dependency FBs** now do too (`MotorDOL`/`EquipmentControlSystem`/
`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem` since S1 item 19; `AirStar` since S1 item 23;
`MotorVSDSystem` since S1 item 24; `TomraControlSystem` since this item) — **no known remaining gaps in
`PlantAutoControl`'s dependency FBs.** Full story for each closed item: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 item 25 — `SWAP` (byte-swap box instruction) — implementation + tests + docs + live verification DONE, NOT YET COMMITTED

**Status as of 2026-07-13: fully implemented, tested (244/244 converter tests green, up from 236),
live-verified against real data (a genuine milestone — `TomraControlSystem` now fully round-trips, the
eighth and final of `PlantAutoControl`'s 8 dependency FBs), and documented (`ir/SPEC.md`,
`src/converter/README.md`, `docs/notes/stage-gates.md`, `CHANGELOG.md` all updated this pass).
Waiting on explicit "commit this" from the project owner before committing** — per this session's
established discipline, never auto-commit.

**Picked up per the project owner's own explicit choice** ("Commit this, then let's ground Swap
for TomraControlSystem") — deliberately scoped narrower than recent items (grounding only, not "build"),
signaling the project owner wanted to review the real shape before committing to a design.

**Grounded against real `TomraControlSystem`** (scratch temp, deleted after use) — 2 independent
instances, identical shape:
```xml
<Part Name="Swap" UId="34" DisabledENO="true">
  <TemplateValue Name="SrcType" Type="Type">Word</TemplateValue>
</Part>
```
Full network context: `Contact -> Swap -> tag`, `en` Contact-gated (same `TraceChain` mechanism as
every other en-gated production), `in`/`out` plain tag `IdentCon`s, no ENO chaining. Structurally
`Convert` minus `DestType` — a byte-swap doesn't change the value's type.

**Design fork surfaced and resolved via `AskUserQuestion` before any code**: standalone
`SwapStatement`/`SwapStatementSidecar` (mirroring `ConvertStatement` minus `DestType`) vs. folding
into `ConvertStatement` with a nullable `DestType`. Project owner chose the standalone type —
keeps each source Part Name mapped to its own IR construct, same precedent as `MulKind`/
`TimerKind` staying separate variants. Project owner then explicitly confirmed proceeding straight
to implementation in the same pass via a second clarifying question.

**Implementation mirrors `Convert`'s own exactly, minus the `DestType` field/line/group, at every
touchpoint**: `Ir.Model` (`SwapStatement`/`SwapStatementSidecar`, `IrNetwork`/`NetworkSidecar`
gain a `Swaps` list), `FlgNetParser` (`SupportedPartNames` gains `"Swap"`, `ParseSwapFixedShape`),
`FlgNetWriter` (`DisabledENO` gate list gains `"Swap"`), `GraphReducer.ReduceSwap`,
`FlgNetBuilder.BuildSwap`, readable-form grammar `SWAP(EN := <expr-or-ENO>, IN := <expr>) =>
<dest>` in both `IrSerializer`/`IrParser`.

**Tests**: `SwapTests.cs` — 8 new tests (mirroring `MulConvertTests.cs`'s own Convert coverage:
parse/reduce/sidecar/round-trip/serialize/full-block, plus two negative tests). New fixture
`SwapFedByRail.xml` (genericized from the real Contact-gated topology). One test-assertion mistake
self-corrected on first `dotnet test` run: a single-Contact-gated `en` reduces to a bare
`Expr.TagRef`, not `Expr.And` wrapping one operand (matches `MoveTests.cs`'s own precedent) —
fixed the assertion, not the code. All 244 converter tests pass (up from 236). `openness-cli` (68)
and golden-harness (11) suites unaffected, confirmed still green.

**Live-verified against real data, 2026-07-13** — fresh `TomraControlSystem` export from the real
`JOB9002_PLC` device: `converter to-ir` **succeeded completely, no errors at all.**
`to-ir → to-xml → to-ir` round-trips **byte-identical**, confirmed via `diff` (redone carefully
with distinct scratch filenames throughout after a same-name-overwrite mistake on the first
attempt clobbered the file needed for comparison — caught before it affected any real conclusion).
Confirmed genuinely exercising this item's own work via direct grep of the converted `.ir` text:
both real `Swap` occurrences present (`SWAP(EN := PlantControl.Test[5], IN := ControlWord0) =>
OutputWord0` and the `ControlWord1`/`OutputWord1` sibling). All real exported data deleted from
scratch temp immediately after use, confirmed via `git status --short`.

**`TomraControlSystem` is now the eighth and final of `PlantAutoControl`'s own 8 dependency FBs to fully
round-trip end to end.** All 8 are now fully instruction-level round-trippable — no more known
real-data gaps in this dependency chain. The true TIA-cycle proof for `PlantAutoControl` itself remains
separately open (S1 items 16/17's own finding: it depends on external tags/FBs no other TIA
project has) — unrelated to this item.

**Not yet raised with the project owner**: nothing live remaining from the `PlantAutoControl` dependency
chain specifically — worth flagging that this milestone is complete and asking what's next (the
true TIA-cycle proof for `PlantAutoControl` itself, or a different block/area entirely) once this item
is committed.

**Final verification before presenting for commit — already done this pass**: all three test
suites re-run and confirmed green (244 converter / 68 openness-cli / 11 golden-harness),
`git status --short` confirmed only expected files changed (no real restricted data). Ready to
present for explicit commit approval.

**Note — a mid-session tooling outage, unrelated to project state**: partway through this item's
implementation, the Bash/PowerShell tools became temporarily unavailable due to an Anthropic
auto-mode safety-classifier outage (not a session/context limit, not a code issue) — all code
edits continued via read-only tools and manual review during the outage; `dotnet build`/`test`
resumed working once the classifier recovered, confirming the manual review had been correct
(clean build, no compile errors, only one test-assertion fix needed). No project-specific recovery
action needed if this doc is being read after such a gap — just re-run the verification steps
above.
