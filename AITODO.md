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
`MUL`/`CONVERT` — arithmetic incl. ENO-chaining (committed `ce0be15`). `FC PlantAutoControl` now
converts as a whole block (`to-ir → to-xml → to-ir` byte-identical) — first real production block
this session to fully round-trip. `TONR`/`Add`/`Lt` are **mid-implementation, NOT committed** —
see "Current task" below. Full story for each closed item: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 item 19 — `TONR` + `Add` + `Lt` — implementation + docs + live verification DONE, NOT YET COMMITTED

**Status as of 2026-07-12: mid-implementation, picking back up here after a session pause.** Do
NOT trust any narrative below over `git status --short` / `git diff --stat` in the repo root —
check those first, they are ground truth for what's actually changed on disk. As of the last
edit, `git status --short` shows only these files modified (all uncommitted, no new files yet):
`src/converter/Converter/Ir/Model.cs`, `src/converter/Converter/SimaticMl/FlgNetParser.cs`,
`src/converter/Converter/SimaticMl/FlgNetWriter.cs`, `src/converter/Converter/GraphReducer.cs`,
`src/converter/Converter/SimaticMl/FlgNetBuilder.cs`, `src/converter/Converter/Ir/IrSerializer.cs`
(partial). **The project has not been built or tested since these edits started** — expect it to
be broken until `IrParser.cs` catches up (see below); do not run `dotnet test` expecting green yet.

**Picked up per the project owner's own explicit sequencing** ("Commit this, then plan out what's
next" → chose `TONR` over the FC/FB parameter-interface gap via `AskUserQuestion`, "TONR
(Recommended)"). Planned formally in plan mode
(`C:\Users\User\.claude\plans\quirky-gathering-ripple.md` — **read this file, it's the full
authoritative design record**, not duplicated in full here).

**Phase 0 grounding (done, real data already deleted)**: exported `MotorDOL`/`FilterUnitSystem` from
`JOB9002` (scratch temp, deleted immediately after use both times — confirmed via `git status
--short` showing no stray files). Found:
- `TONR`: identical `Version`/`Instance`/`time_type` shape to `TON`, plus one genuine new port —
  `R` (reset), fed directly by a plain tag `IdentCon` in both instances (no chain, same shape as
  `PT`). No `EN`/`ENO` on either TON or TONR.
- **Scope expanded mid-grounding, confirmed via `AskUserQuestion` ("Bundle Add + Lt into this
  item")**: the same real networks also need `Add` (identical XML shape to `Mul` —
  `DisabledENO="true"`/`Card="2"`/`AutomaticTyped SrcType`) and `Lt` (a third comparison operator,
  identical shape to `Eq`/`Ge`) to fully round-trip — `TONR` alone wouldn't have been enough.
  `Add`'s own `en` in the grounded network is fed by a comparison's (`Lt`'s) `out` — an ordinary
  `TraceChain` `Condition`, **not** ENO-chained — confirmed by a second, brief re-export/check
  (also deleted after use). So `Add` needs NO new `ResolveEnSource` branch, just `Lt` added to the
  boolean-producer dispatch.

**Design (from the plan file, already implemented in code — see file-by-file status below)**:
`TimerKind` (`Ton`/`Tonr`) and `MulKind` (`Multiply`/`Add`) enums, mirroring the existing
`CoilKind` (`Assign`/`Set`/`Reset`) precedent exactly. Both are duplicated onto their sidecar
records too (not left model-only), because `BuildTimer`/`BuildMul` in `FlgNetBuilder.cs` are
sidecar-only (never take the model alongside), unlike `BuildOneChain` — confirmed by reading
`FlgNetBuilder.cs` in full before touching it, not assumed. `Lt` needs zero new model shape at
all — `ChainStepSidecar.CompareStep` already carries `PartName` generically.

**File-by-file status (code, not yet tested)**:
- `src/converter/Converter/Ir/Model.cs` — **DONE.** `TimerKind` enum; `TimerBinding` gained
  `Kind`/`Reset` (`Expr?`); `TimerBindingSidecar` gained `Kind`/`Reset` (`OperandSidecar?`).
  `MulKind` enum; `MulStatement` gained `Kind`; `MulStatementSidecar` gained `Kind`.
- `src/converter/Converter/SimaticMl/FlgNetParser.cs` — **DONE.** `SupportedPartNames` gained
  `"TONR"`/`"Lt"`/`"Add"`; `SupportedComparisonPartNames` gained `"Lt"`; dispatch generalized
  (`name is "TON" or "TONR"`, `name is "Mul" or "Add"`); `ParseTon`/`ParseMulFixedShape` both now
  take a `partName` parameter (used in their own error messages and passed to
  `ParseInstanceReference`/`ParseCardinality`) instead of hardcoding `"TON"`/`"Mul"`.
- `src/converter/Converter/SimaticMl/FlgNetWriter.cs` — **DONE.** `DisabledENO="true"` gate list
  gained `"Add"`. Version/Instance/time_type/Cardinality/AutomaticTyped writing branches were
  already field-presence-gated (not Part-Name-gated) so `TONR`/`Lt` needed no changes there.
- `src/converter/Converter/GraphReducer.cs` — **DONE.** `tonParts`/`mulParts` collection filters
  generalized (`Name is "TON" or "TONR"` / `"Mul" or "Add"`); new `TimerKindFor`/`MulKindFor`
  helpers (mirror `CoilKindFor`); `ReduceTimer` resolves `R` via `ResolveTagOrLiteralOperand` when
  `Kind == Tonr`; `ReduceMul` tags the result with `Kind`; `OutPortFor` gained `"Lt" => "out"` and
  `"TONR" => "Q"`; `ComparisonOperator` gained `"Lt" => "<"`; `TraceChain`'s two upstream-dispatch
  checks generalized (`upstreamPart.Name is "TON" or "TONR"`, `is "Eq" or "Ge" or "Lt"`).
  `ResolveEnSource`'s ENO-chain check (`precedingPart.Name is "Mul" or "Convert"`) deliberately
  **NOT** touched — no live evidence `Add` ever participates in ENO-chaining, so it stays out
  rather than being guessed in.
- `src/converter/Converter/SimaticMl/FlgNetBuilder.cs` — **DONE.** `BuildTimer` now builds the `R`
  wire when `sidecar.Reset` is non-null, and picks the Part Name via a new `TimerPartNameFor`
  helper (mirrors `CoilPartNameFor`) instead of hardcoding `"TON"`. `BuildMul` picks the Part Name
  via a new `MulPartNameFor` helper instead of hardcoding `"Mul"`.
- `src/converter/Converter/Ir/IrSerializer.cs` — **PARTIAL.** Readable-form: `network.Timers` loop
  now emits `TON(...)`/`TONR(...)` via new `TimerKeywordFor` helper, and appends `, R := <expr>`
  when `timer.Reset` is set; `network.Muls` loop now emits `MUL(...)`/`ADD(...)` via new
  `MulKeywordFor` helper. Both helper methods added (mirror `CoilKeywordFor`).
  **NOT YET DONE — this is the exact next step**: `SerializeSidecarNetwork`'s own `timer`/`mul`
  blocks (around the `"  timer "`/`"  mul "` lines) still don't emit a `kind = ton/tonr` (or
  `mul/add`) line or a `reset` operand line — the sidecar text format needs both before parsing
  can round-trip. Without this, `Kind`/`Reset` only survive in the readable IR text, not the
  sidecar, so `to-ir → to-xml` would silently lose which Part Name to regenerate whenever the
  readable-form keyword alone isn't re-derived by the parser (it should be, via
  `TimerKindFor`/`MulKindFor`-equivalent parsing — but the sidecar should still carry it
  explicitly, matching how every other Kind-bearing sidecar in this codebase does, e.g.
  `MulStatementSidecar.Kind` itself, already done above).
- `src/converter/Converter/Ir/IrParser.cs` — **NOT STARTED.** Needs: recognize `TON(`/`TONR(` as
  two distinct header regexes (or one regex + a capture group) instead of the current
  hardcoded-`TON(`-only loop condition/regex; parse the optional `, R := <expr>` trailing
  argument; recognize `MUL(`/`ADD(` the same way; parse the new sidecar `kind =`/`reset` lines
  once `IrSerializer.cs` emits them.
- **Tests**: **NOT STARTED.** No `TonrTests.cs`, no new fixtures, no `Add`/`Lt` test coverage yet.
- **Docs**: **NOT STARTED.** `ir/SPEC.md`, `src/converter/README.md`, `docs/notes/stage-gates.md`,
  `CHANGELOG.md` all still describe the pre-S1-item-19 state (S1 item 18 as the latest closed
  item) — none updated for this item yet.
- **Live verification**: **NOT STARTED** since the code edits began (the Phase 0 grounding runs
  earlier are exploratory/read-only, not the "does the built pipeline actually round-trip this"
  check every prior item does at the end). Still to do once the code compiles and tests pass:
  isolate the real `TONR`/`Add`/`Lt`-containing networks in `MotorDOL`/`FilterUnitSystem` in-memory, then
  attempt a whole-block `to-ir` sweep of all 5 previously-`TONR`-blocked FBs
  (`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem`) to see whether they now
  fully round-trip or hit a further gap — report honestly either way.

**Update, resumed same day**: `IrSerializer.cs`/`IrParser.cs` both finished (readable-form
`TON`/`TONR`/`MUL`/`ADD` keyword dispatch on read+write, `R := <expr>` argument, sidecar
`kind`/`reset` lines on read+write). `dotnet build` is clean (0 errors/warnings) and the full
existing suite (191/191 converter tests) still passes unchanged — the whole pipeline compiles and
nothing regressed. **All 7 pipeline-layer files are now done**: `Model.cs`, `FlgNetParser.cs`,
`FlgNetWriter.cs`, `GraphReducer.cs`, `FlgNetBuilder.cs`, `IrSerializer.cs`, `IrParser.cs`.

**Status as of 2026-07-12: fully implemented, tested (207/207 converter tests green), live-verified
against real data, and documented (`ir/SPEC.md`, `src/converter/README.md`,
`docs/notes/stage-gates.md`, `CHANGELOG.md` all updated this pass). Waiting on explicit "commit
this" from the project owner before committing** — per this session's established discipline,
never auto-commit.

`Converter.Tests/TonrTests.cs` written (16 new tests: `TONR` parse/reduce/round-trip/serialize/
full-block, a `TON`-alongside-`TONR` mixed-kind test, `Lt` parse/reduce/round-trip/serialize,
`Add` parse/reduce/round-trip/serialize/full-block — including the confirmed-real "`Add`'s `en`
fed by `Lt`'s `out` is an ordinary Condition, not ENO-chained" finding as its own explicit test).
Three new fixtures: `WithTonr.xml`, `LtFeedsCoil.xml`, `AddFedByComparison.xml` (genericized from
the real grounded `MotorDOL`/`FilterUnitSystem` shape — real tag names like `HrTotaliserTimer` were NOT
reused verbatim, invented generic names used instead, per the data-boundary discipline).

**Live-verified against real data, 2026-07-12**: exported all 5 previously-`TONR`-blocked
dependency FBs fresh (`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem`) and
ran `converter to-ir` on each — **all 5 fully converted**, confirmed via grep counts (exactly one
`TONR(`/`ADD(`/`<`-operator per block) that this genuinely exercises the new code, not a lucky
no-op. `MotorDOL`/`FilterUnitSystem` (the two directly grounded) were additionally carried through a
full `to-ir → to-xml → to-ir` cycle — **byte-identical both times**. All real exported data
deleted from scratch temp immediately after use, confirmed via `git status --short` showing no
stray files.

**Bottom line**: combined with S1 item 18, this closes every real *instruction-level* gap the
original 8-FB dependency sweep found — all 5 of `PlantAutoControl`'s dependency FBs blocked by
`Mul`/`Convert`/`TONR` now fully round-trip as whole blocks. The remaining 3
(`TomraControlSystem`/`MotorVSDSystem`/`AirStar`) are blocked only by the separate, already-known, larger
FC/FB parameter-interface modeling item — not addressed by this item, not attempted.

**Not yet raised with the project owner, pending this item's commit**: what to scope next. The
FC/FB parameter-interface gap (blocks the remaining 3 dependency FBs) is now the only known
real gap left from the original 8-FB sweep — but it's already flagged repeatedly as "separate,
larger, already-known deferred item" (ADR-0001 decided against inline interface snapshots at
call sites), so it likely needs its own plan-mode pass rather than a quick extension. No decision
made; should be asked once this item is closed, not assumed.

**Final verification before presenting for commit — still to do on resume if not already done**:
re-run all three test suites (`src/converter`/`src/openness-cli`/`tests/golden`) one final time to
reconfirm green after the doc-only edits, `git status --short` to confirm only expected files
changed, then present for explicit commit approval.
