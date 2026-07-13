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
`openness-cli` gained block deletion, confirmed `import`-overwrite behavior, an Openness API
surface survey (`b20cb37`), safe concurrent Portal sessions on different projects (`4841bf5`), a
fix for a Portal-instance-pileup bug in that same feature (`8042648`), and now UDT/PLC-data-type
support (S1 item 26, done, tested, live-verified — see below, not yet committed). Full story for
each closed item: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: UDT (PLC data type) support — DONE, TESTED, LIVE-VERIFIED, NOT YET COMMITTED

**Status as of 2026-07-14: fully implemented, documented, and live-verified end to end. The
Phase 0 grounding blocker noted in earlier revisions of this doc (a second concurrent Portal
instance failing to connect) resolved itself on a fresh retry the next session turn — see
`docs/notes/openness-quirks.md`'s own "transient — resolved on retry" update.**

**Implementation**: full converter pipeline (`PlcTypeSource`/`PlcTypeSourceParser`/
`PlcTypeSourceWriter`/`TypeIr.cs`, `DbInterfaceMembers.ParseTypeMember`/`WriteTypeMember`,
`Sanitizer.ApplyToType`) plus the `openness-cli` `--type` plumbing already built the prior session
(`ExportType`/`ImportTypes`/`CompileType`, `--type` on `export`/`import`/`compile`). 10 new
converter tests + 6 new openness-cli tests — **254 converter, 79 openness-cli, 11 golden-harness,
all green.**

**Live-verified, 2026-07-14**: real `TypeDOL` exported from `JOB9002_PLC`, round-tripped
byte-structurally identical, sanitized (`sanitization/TypeDOL.map.json`, reusing the established
`TypeDOL`→`MotorIOSet` name), imported and compiled cleanly in `SampleProject`. **Re-importing the
already-sanitized `MotorDOL.sanitized.xml` no longer hits `Data type "MotorIOSet" is unknown`** —
the original blocker this item was built to resolve is closed.

**Docs finished**: `src/converter/README.md` (new "UDT / PLC data type support" section),
`src/openness-cli/README.md` (`--type` documented on export/import/compile),
`docs/notes/stage-gates.md` (S1 item 26), `CHANGELOG.md`, `ir/SPEC.md` (`TYPE` grammar),
`docs/notes/openness-quirks.md` (corrected the stale "unresolved" framing).

**Not committed** — waiting for the project owner's own explicit "commit this," per this session's
unbroken discipline.

## New open item, found during UDT live verification, NOT YET FIXED: `MotorDOL` Part-ordering bug

Re-importing `MotorDOL` itself (after its `TypeDOL`/`MotorIOSet` dependency was resolved) hit a
**new, separate** error: `"The elements must be sorted according to the current flow"` at
`Part UId=49` (an `RCoil`, network 3, "Start Signal After Inhibit/Fault") — TIA's own `Import()`
validator apparently expects Parts/Wires listed in some data-flow-topological order this
converter's `FlgNetWriter` doesn't currently guarantee. Confirmed unrelated to UDT support or
sanitization (sanitization only renames identifiers, never reorders XML). Per hard rule 7, not
hand-patched — the failed import rolled back cleanly, `MotorIOSet` itself stayed present and
compilable. Not investigated further this pass; needs to be raised with the project owner as its
own item once UDT support is committed. Full detail: `docs/notes/stage-gates.md` ("S1 item 26").

## Closed, committed: `openness-cli` Portal-instance-pileup bug fix

Committed as `8042648`. Full story: `docs/notes/stage-gates.md` ("`openness-cli`: fixing a real
instance-pileup bug in the fix above").

## Full import+compile cycle test for `PlantAutoControl`'s dependency FBs — superseded by the UDT item above

The original blocker this task paused on (`Data type "MotorIOSet" is unknown`, hit importing
`MotorDOL`) is exactly what UDT support (S1 item 26, above) was built to resolve — and it did.
The task itself isn't fully closed though: resolving the UDT dependency uncovered the new, separate
`MotorDOL` Part-ordering bug tracked above. Not resuming further per-FB testing until that's
addressed or the project owner decides otherwise.

**Scratch state to clean up once UDT support is committed**: `sanitization/MotorDOL.map.json` and
`sanitization/TypeDOL.map.json` are real, reusable artifacts (gitignored, not committed) — kept.
`$CLAUDE_JOB_DIR/tmp/autocontrol_fullcycle/` and `$CLAUDE_JOB_DIR/tmp/udt_grounding/` still hold
real `JOB9002`-derived content and must be deleted before this item is considered fully closed.
