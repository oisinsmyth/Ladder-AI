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

**S1 — Lossless round-trip, ACTIVE.** See `docs/notes/stage-gates.md` for full gate history. All
8 of `PlantAutoControl`'s dependency FBs are IR-complete (`to-ir → to-xml → to-ir` byte-identical).
UDT/PLC-data-type support (S1 item 26) is committed (`557d35c`). `openness-cli`'s concurrent-Portal
stability audit (two real bugs found and fixed) is committed (`2829ec1`). `FlgNetBuilder`'s
Part-ordering bug (blocking `MotorDOL`'s own import) is fixed and live-verified — see below, not
yet committed. Full story for each closed item: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: `FlgNetBuilder` Part-ordering fix — DONE, TESTED, LIVE-VERIFIED, NOT YET COMMITTED

**Status as of 2026-07-14.** Resumed from `RESUME-FlgNetBuilder-Timer-Ordering.md` (now deleted,
folded into `docs/notes/stage-gates.md` "S1 item 26 continued"). Two real bugs in `FlgNetBuilder`,
both fixed:

1. `OrStep`'s own Part was emitted before its branches — fixed (mirrors `NotStep`'s own
   children-then-self order).
2. Timer builds were phase-hoisted (all built in one global phase ahead of everything else) rather
   than inline with the production that needs them — real TIA export order is fully contiguous per
   rung, not grouped by construct type. Fixed with a new `EnsureTimerBuilt` helper that builds a
   Timer the first time a `TimerOutputStep` reaches it, contiguous with that production; a
   catch-all pass still handles Timers never referenced this way.

**Live-verified**: regenerated `MotorDOL`'s sanitized XML fresh, confirmed Parts order now matches
the real TIA export exactly, and **the import into `SampleProject` succeeded** —
`MotorStarter` (sanitized `MotorDOL`) is now FB2 there, the blocker this whole sub-investigation
existed to resolve. 254/254 converter tests, 11/11 golden-harness, all pass.

**A new, separate, expected finding on compile, not a regression**: `compile --block MotorStarter`
fails with `"Missing instance DB"` (Networks 3/8) — the same category of gap already documented for
`PlantAutoControl` itself (S1 items 16/17): an isolated FB's own `LocalVariable`-scoped timer instances
need a calling context (an instance DB, or a calling FC) to resolve, which `SampleProject` doesn't
have for `MotorDOL`. Not a converter bug, not investigated further.

**Not committed** — waiting for the project owner's own explicit "commit this."

## Full import+compile cycle test for `PlantAutoControl`'s dependency FBs — Part-ordering blocker resolved; blocked on missing-instance-DB now

The `Data type "MotorIOSet" is unknown` blocker (S1 item 26) and the Part-ordering blocker
(above) are both resolved. `MotorDOL` now imports cleanly into `SampleProject`. The remaining
blocker for a full compile is the "missing instance DB" gap above — same category as `PlantAutoControl`
itself's own known dependency gaps (S1 items 16/17). Not resuming further per-FB testing unless the
project owner wants to pursue building out a calling context for this.

**Scratch state**: `sanitization/MotorDOL.map.json` and `sanitization/TypeDOL.map.json` are real,
reusable artifacts (gitignored, not committed) — kept.
`$CLAUDE_JOB_DIR/tmp/autocontrol_fullcycle/MotorDOL.fresh.xml` is real, unsanitized `JOB9002`-derived
content (the ground-truth export used to fix the Part-ordering bug) — should be deleted once this
item is fully closed out. The `.sanitized.xml`/`.sanitized.ir` siblings are sanitized, non-identifying,
safe to keep. `$CLAUDE_JOB_DIR/tmp/udt_grounding/` holds only sanitized content, safe to keep.

## Current task: PLC tag table support (`TAGTABLE`) — DONE, TESTED, LIVE-VERIFIED, NOT YET COMMITTED

**Status as of 2026-07-14.** Task #127, spun out of Phase 0.1 grounding for the `PlantAutoControl`
round-trip plan (`docs/notes/stage-gates.md` / the approved plan): `PlantAutoControl` references 10 tags
(`Tag_45`-`Tag_54`) that turned out to be genuine PLC tag-table entries, not DB members — a
construct never touched before. Built end to end: converter parse/write/IR/sanitize
(`PlcTagTableModel.cs`, `PlcTagTableSourceParser/Writer.cs`, `Ir/TagTableIr.cs`,
`Sanitizer.ApplyToTagTable`) and `openness-cli` support (`EnumerateTagTables`/`ExportTagTable`/
`ImportTagTables`, `list --tagtables`, `export`/`import --tagtable`). Deliberately minimal
(project owner's own call) — full intentional-gaps list in `src/converter/README.md`, "PLC tag
table support", and `ir/SPEC.md`'s "Tag tables, UDTs, DBs" section.

**Live-verified, both directions**: `export --tagtable "Default tag table" --device JOB9002_PLC`
against `JOB9002` succeeded first try. Built a minimal 10-tag table (filtered the real export's own
IR text down to `Tag_45`-`Tag_54`, not a hand-sanitized full 900+-tag table). `import --tagtable`
into `SampleProject`'s root tag-table group succeeded first try; `SampleProject` now has "Default
tag table" (10 tags) as a real, growing part of `PlantAutoControl`'s dependency closure (Phase 2
groundwork, not scratch mess — left in place deliberately). 262/262 converter tests, 96/96
`openness-cli` tests.

**Not committed** — waiting for the project owner's own explicit "commit this."

**Scratch state, needs cleanup once committed**: `$CLAUDE_JOB_DIR/tmp/autocontrol_fullcycle/
PlantAutoControl.fresh.xml` and `DefaultTagTable.fresh.xml` are real, unsanitized, full-size `JOB9002`
content (the full block and the full 900+-tag table) — delete once this item is fully closed out.
`MinimalTagTable.raw.ir`/`.raw.xml` carry only the 10 real tag names/addresses (`Tag_45`-`Tag_54`,
`%IW64`-`%IW78`/`%QW64`-`%QW66`) — judged non-identifying (Siemens auto-generated placeholder
names, no site business content), safe to keep; not run through `Sanitizer` for that reason,
noted explicitly in `src/converter/README.md`'s gaps list rather than left silent.

**Next**: resume the approved plan (`docs/notes/stage-gates.md`/task list #122-#126) — Phase 0.2
(ground `CreateInstanceDB` live against `MotorStarter`), Phase 0.3 (formally close out — already
effectively answered via this session's CALL-instance grounding), Phase 1 (remaining 7 dependency
FBs), Phase 2 (bulk DB/tag-table closure for `PlantAutoControl`'s other ~26 roots), Phase 3
(`PlantAutoControl` itself).
