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
live-proven), comparisons (Eq/Ge/Lt/Ne), MOVE (fan-out taps + telescoping dedup), WAND (bitwise
word AND; closes out the AND-merge open item as **confirmed non-existent**), `Not` — standalone
boolean inverter, `CALL` — FB/FC block calls, `SCoil`/`RCoil` — set/reset coils, network/block-level
`Title`, `MUL`/`CONVERT`/`ADD` (arithmetic incl. ENO-chaining), FC/FB parameter-interface modeling
(`Input`/`Output`/`InOut`/`Constant`, committed `500dc68`), the `Mul`/`SrcType` fix (committed
`eaf93b0`), `Access Scope="LocalConstant"` (committed `55b5cb2`), `Ne` (not-equal comparison) —
**implemented/tested/documented, NOT YET COMMITTED, see "Current task" below**. `FC PlantAutoControl`
now converts as a whole block (`to-ir → to-xml → to-ir` byte-identical) — first real site
block this session to fully round-trip; `MotorDOL`/`FilterUnitSystem` (2 of its 8 dependency FBs) now do
too. Full story for each closed item: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S1 item 22 — `Ne` (not-equal comparison) — implementation + tests + docs DONE, NOT YET COMMITTED

**Status as of 2026-07-12: fully implemented, tested (225/225 converter tests green), live-verified
against real data, and documented (`ir/SPEC.md`, `src/converter/README.md`,
`docs/notes/stage-gates.md`, `CHANGELOG.md` all updated this pass). Waiting on explicit "commit
this" from the project owner before committing** — per this session's established discipline,
never auto-commit.

**Picked up per the project owner's own explicit choice** — asked directly what `Ne` was likely to
be (found blocking `AirStar` during S1 item 21's own live verification). Answered before any
grounding, from prior evidence already in the repo: `ir/SPEC.md`'s own readable-form table already
had a row noting `Ne`/`Le`/`Lt` as "confirmed real Part Names... not yet built," and `IrParser`'s
own `ComparisonTokens` array already carried the `<>` token, unused, waiting for exactly this — the
not-equal sibling of `Eq`(`=`)/`Ge`(`>=`)/`Lt`(`<`), all three already fully supported.

**Grounded directly against real data** (real `FB AirStar`) rather than a fresh formal plan-mode
cycle — small, well-precedented, third comparison-family addition this session (after `Eq`/`Ge`'s
original build and `Lt`'s own S1 item 19 addition). Found:
```xml
<Part Name="Ne" UId="54">
  <TemplateValue Name="SrcType" Type="Type">Int</TemplateValue>
</Part>
```
Identical shape to `Eq`/`Ge`/`Lt` — same `SrcType` `TemplateValue`, same `pre`/`in1`/`in2`/`out`
ports, confirmed by tracing the real wires within that specific network. Also spotted in passing
(not chased): a `Part Name="TOF"` (off-delay timer) — a new, separate, unaddressed gap.

**Design**: `FlgNetParser.SupportedPartNames`/`SupportedComparisonPartNames` and `GraphReducer`'s
`OutPortFor`/`ComparisonOperator`/`TraceChain` upstream-dispatch each gained a fourth case.
**Zero changes needed anywhere downstream of reduction** — `ChainStepSidecar.CompareStep.
PartName`/`Expr.Compare.Operator` are both carried verbatim, not derived from a hardcoded switch,
so `FlgNetBuilder`/`FlgNetWriter`/`IrSerializer`/`IrParser` already handle any confirmed
comparison Part Name generically. Repurposed the one existing test that used `"Ne"` as its own
placeholder for an unconfirmed Part Name — swapped to `"Le"` (still genuinely unconfirmed).

**File-by-file status (all done)**: `SimaticMl/FlgNetParser.cs` (`SupportedPartNames`/
`SupportedComparisonPartNames` gain `"Ne"`, hard-error message updated), `GraphReducer.cs`
(`OutPortFor`/`ComparisonOperator`/`TraceChain` upstream check each gain a `"Ne"` case).

**Tests**: `ComparisonTests.cs` — 4 new tests (parse/reduce/round-trip/serialize for `Ne`, mirroring
the `Lt`/`Ge` fixture pattern), plus the repurposed negative test (now `"Le"`). New fixture
`NeFeedsCoil.xml`. All 225 converter tests pass (up from 221).

**Live-verified against real data, 2026-07-12**: exported `AirStar` fresh and ran `converter to-ir`
— **the `Ne` error is gone**. Doesn't fully round-trip as a whole block yet — progresses to `TOF`
(spotted alongside `Ne` during this item's own grounding — a new, separate, unaddressed gap, not
chased here). All real exported data deleted from scratch temp immediately after use, confirmed
via `git status --short`.

**Bottom line**: `Ne` is built, tested, and live-verified — the third and smallest comparison-
family addition this session (zero changes needed anywhere downstream of `GraphReducer`'s own
reduction dispatch). `AirStar` still doesn't fully round-trip (now blocked by `TOF`, not addressed
here); `MotorVSDSystem`'s own remaining `<Call>`-missing-`<Instance>` gap is also still open, unrelated.

**Not yet raised with the project owner, pending this item's commit — three live candidates now**:
1. `Swap` (blocks `TomraControlSystem`) — not grounded at the XML-shape level yet.
2. `<Call>` missing `<Instance>` (blocks `MotorVSDSystem`) — not grounded yet.
3. `TOF` (blocks `AirStar`) — an off-delay timer, likely structurally close to `TON`/`TONR` (same
   `Version`/`Instance`/`time_type` family), similar in spirit to how `TONR` itself was added in
   S1 item 19 — not grounded yet, but a plausible next "cheap-ish, well-precedented" pick.
No decision made; should be asked once this item is closed, not assumed.

**Final verification before presenting for commit — still to do on resume if not already done**:
re-run all three test suites (`src/converter`/`src/openness-cli`/`tests/golden`) one final time to
reconfirm green, `git status --short` to confirm only expected files changed, then present for
explicit commit approval.
