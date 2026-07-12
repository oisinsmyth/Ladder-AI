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
committed `32e5228`), `SWAP` (byte-swap box instruction, committed `2c61b0b`). `FC PlantAutoControl`
now converts as a whole block (`to-ir → to-xml → to-ir` byte-identical) — first real site
block this session to fully round-trip; **all 8 of its dependency FBs** now do too — **no known
remaining gaps in `PlantAutoControl`'s dependency FBs.** Full story for each closed item:
`docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: `openness-cli` — block deletion, import-overwrite confirmation, API surface survey — implementation + live verification DONE, NOT YET COMMITTED

**Status as of 2026-07-13: fully implemented, tested (73/73 openness-cli tests green, up from
68; converter/golden-harness unaffected at 244/11), live-verified against real data (both
`import`'s overwrite behavior and the new `delete` command, against `SampleProject` only), and
documented (`src/openness-cli/README.md`, `docs/notes/openness-api-surface-v20.md`,
`docs/notes/openness-quirks.md`, `docs/notes/stage-gates.md`, `CHANGELOG.md` all updated this
pass). Waiting on explicit "commit this" from the project owner before committing** — per this
session's established discipline, never auto-commit.

**Picked up per the project owner's own explicit ask**, after all 8 of `PlantAutoControl`'s dependency
FBs closed: `openness-cli` had no way to delete a block (flagged as a real gap back in S1 items
16/17 — an uncompiled `PlantAutoControl` block was left in `SampleProject` after a cross-project import
test with no way to clean it up programmatically), whether `import` actually overwrites a
pre-existing block needed live confirmation (an implicit "update" capability), and the project
owner wanted a broader survey of unused Openness API surface. Went through a full formal plan
(`EnterPlanMode`/`ExitPlanMode`) given the scope.

**Research** (read the installed V20 `Siemens.Engineering.xml` doc-comments file directly, real
prose descriptions not just reflected type shapes): confirmed `PlcBlock.Delete()` exists
("Deletes this instance.", no arguments) and `ImportOptions.Override` is documented "Override
existing" (`None` = "Throw if exists"). Broader survey (scoped to `SW.Blocks`) found several other
unused members — full list in `docs/notes/openness-api-surface-v20.md`'s new "SW.Blocks survey"
section: `SWImportOptions` (relevant-looking but doesn't fix the cross-project compile blocker
from S1 items 16/17), `Find`/`Create` (direct group lookup/creation, unused), `CreateFB`/
`CreateInstanceDB`/`CreateFrom` (S6+ logic-generation scope, not buildable now), `GetAttribute`/
`SetAttribute` (generic block metadata read/write, unused).

**Design decision, resolved via `AskUserQuestion` in plan mode**: `delete` requires an explicit
`--yes` flag before actually deleting. Without it, resolves the block and prints what it *would*
delete, exits a new `ExitCodes.NotConfirmed (10)`, touches nothing — a new safety pattern for this
codebase (every other subcommand acts immediately), since delete is the first genuinely
irreversible operation this CLI exposes and has no undo.

**Implementation**: `IOpennessGateway.DeleteBlock(blockName, deviceFilter, confirm)` mirrors
`ExportBlock`/`CompileBlock` exactly (`FindMatchingBlocks`, `--device` disambiguation,
`SafetyContentRefusedException` for safety-classified blocks). New `delete` subcommand end to end
(`ArgumentParser.cs`: `DeleteCommandOptions`, `ParseResult.DeleteSuccess`, `ParseDelete`;
`Program.cs`: `RunDelete`, new `ExitCodes.NotConfirmed`). 7 new argument-parser tests
(`ExportImportCompileArgumentParserTests.cs`, mirroring the existing `export` coverage) — the
gateway method itself isn't unit-testable (same reason no `OpennessGateway` method is —
Siemens.Engineering's COM-backed types can't be faked), verified live only.

**Live-verified against real data, 2026-07-13 — both against `SampleProject` only, never
`JOB9002`**:
- **Import-overwrite ("update") confirmation** — fully reversible, Green-tier only (used the
  already-committed `TimerSample` reference-corpus block, not `JOB9002` content): exported fresh,
  made one trivial edit (a network `Title` → `"OVERWRITE TEST MARKER"`), re-imported with
  `Override` — succeeded, `list` confirmed still exactly one `TimerSample` block. Re-export
  initially refused (`"Inconsistent blocks... cannot be exported"` — the already-documented
  `IsConsistent` quirk); `compile --block TimerSample` cleared it in one call. Re-exported: **the
  test marker was present** — confirmed genuine in-place overwrite. Restored the original content
  the same way; diffed against the very first export: byte-identical except the export's own
  `<Created>` timestamp — `SampleProject` left in its exact original state.
- **Delete** — real cleanup of already-known cruft: the `PlantAutoControl` block left uncompiled in
  `SampleProject` since S1 items 16/17. `list` confirmed present. `delete --block PlantAutoControl` (no
  `--yes`) correctly dry-ran (exit 10, still present after). The confirmed delete was initially
  blocked by Claude Code's own auto-mode safety classifier ("Irreversible Deletion" — the
  *approved plan* had named `PlantAutoControl`, but the classifier requires the user to name the
  resource directly) — stopped and asked rather than working around it; project owner confirmed
  explicitly ("Yes delete PlantAutoControl from the sample project"), then `delete --block PlantAutoControl
  --yes` succeeded, confirmed gone via a final `list` — `SampleProject` back to a clean 10-block
  state.

All three suites green throughout: 73 openness-cli (up from 68), 244 converter, 11 golden-harness.
`git status --short` after each live pass confirmed only expected code/test files changed.

**Not yet raised with the project owner**: with the delete/update tooling now in place, the
natural next real-data step (the project owner's own earlier follow-up question) is attempting a
live TIA import+compile cycle for one or more of `PlantAutoControl`'s individual dependency FBs into
`SampleProject`, now that IR-completeness is confirmed for all 8 — genuinely unconfirmed whether
any of them (especially the smaller ones, e.g. `TomraControlSystem`) would actually compile there
without the full `JOB9002` tag table/FB library, unlike `PlantAutoControl` itself. Worth asking once this
item is committed, not assumed.

**Final verification before presenting for commit — already done this pass**: all three test
suites re-run and confirmed green, `git status --short` confirmed only expected files changed (no
real restricted data, no data-boundary concerns — `SampleProject` is this project's own Green-tier
reference project). Ready to present for explicit commit approval.
