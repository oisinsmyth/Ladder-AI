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
stability work (safe concurrent sessions, pileup fix, and now a full stability audit with two more
real bugs found and fixed) is done and tested but **not yet committed** — see below. Full story for
each closed item: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: `openness-cli` concurrent-Portal stability audit — DONE, TESTED, LIVE-VERIFIED, NOT YET COMMITTED

**Status as of 2026-07-14.** Project owner asked for a rigorous test schedule after the "second
Portal instance sometimes won't connect" symptom recurred twice in one session — worried the
concurrent-session feature itself (`4841bf5`/`8042648`) was unstable. Full walkthrough recorded in
`docs/notes/concurrent-portal-test-plan.md`; authoritative closing record in
`docs/notes/stage-gates.md` ("concurrent-Portal stability audit").

**Verdict: the concurrent-session feature is not the cause of instability** — every stress scenario
tried (two-party concurrency, same-project refusal, killed-mid-flight client, 5 back-to-back fresh
launches from a clean baseline) behaved correctly. The "won't connect" symptom correlates with
stale-process pileup, not concurrency.

**Two real bugs found and fixed along the way, both closed same-day, neither left as a follow-up**:
1. `OpenProject()` was silently repurposing a human's own freshly-launched, empty Portal window —
   fixed (`_connectLaunchedFreshInstance` tracking), retested live with the project owner watching.
2. A client killed mid-launch left a permanently orphaned process behind — fixed via a new
   `LaunchedInstanceRegistry` (persists which PIDs this tool itself launched, across invocations;
   an empty process is only ever reused if positively confirmed as this tool's own). Required
   correcting two errors in `docs/notes/openness-api-surface-v20.md`'s original 2026-07-10 survey
   (missed `TiaPortalProcess.Id` entirely; had `GetCurrentProcess()`'s return type wrong).
   Live-verified end to end by direct construction (a real kill-timing race proved impractical to
   hit externally with the needed precision — see stage-gates.md for why).

**89/89 openness-cli tests pass** (up from 79). Docs updated same session:
`docs/notes/concurrent-portal-test-plan.md` (new), `docs/notes/openness-quirks.md` (closing entry),
`docs/notes/openness-api-surface-v20.md` (corrections), `CLAUDE.md` (environment note),
`docs/notes/stage-gates.md` (dated entry).

**Not committed** — waiting for the project owner's own explicit "commit this."

**Unrelated, separate, still in progress — do not conflate with the above**: a `FlgNetBuilder`
Timer/Part-ordering refactor (found via `MotorDOL`'s own Part-ordering bug, tracked below) is
mid-flight and **does not currently compile**. Full resume context:
`RESUME-FlgNetBuilder-Timer-Ordering.md` (repo root) — read that file first before touching
`src/converter/Converter/SimaticMl/FlgNetBuilder.cs` again.

## Open item, found during S1 item 26's UDT live verification, NOT YET FIXED: `MotorDOL` Part-ordering bug

Re-importing `MotorDOL` itself (after its `TypeDOL`/`MotorIOSet` dependency was resolved) hit a
**new, separate** error: `"The elements must be sorted according to the current flow"` at
`Part UId=49` (an `RCoil`, network 3, "Start Signal After Inhibit/Fault") — TIA's own `Import()`
validator apparently expects Parts/Wires listed in some data-flow-topological order this
converter's `FlgNetWriter` doesn't currently guarantee. **Actively being worked** — see
`RESUME-FlgNetBuilder-Timer-Ordering.md` for exact status (one real sub-bug already fixed and
confirmed correct — `OrStep` Part ordering; a second, deeper root cause identified — Timer builds
are phase-hoisted rather than built inline with the production that triggers them — fix designed
and partially implemented, file does not currently compile). Full grounding detail:
`docs/notes/stage-gates.md` ("S1 item 26").

## Full import+compile cycle test for `PlantAutoControl`'s dependency FBs — blocked on the FlgNetBuilder work above

The original blocker (`Data type "MotorIOSet" is unknown`) is resolved (S1 item 26). The remaining
blocker is the `MotorDOL` Part-ordering bug above, actively being fixed. Not resuming further
per-FB testing until that's finished.

**Scratch state**: `sanitization/MotorDOL.map.json` and `sanitization/TypeDOL.map.json` are real,
reusable artifacts (gitignored, not committed) — kept. `$CLAUDE_JOB_DIR/tmp/autocontrol_fullcycle/`
and `$CLAUDE_JOB_DIR/tmp/udt_grounding/` hold a mix of real `JOB9002`-derived content (some raw,
should be deleted once no longer needed for grounding) and sanitized artifacts (safe to keep) —
see `RESUME-FlgNetBuilder-Timer-Ordering.md`'s own "Scratch files" section for the exact
per-file breakdown.
