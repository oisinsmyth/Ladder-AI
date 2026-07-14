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

**S3 — Comment generation, DONE — gate reviewed and signed off by the project owner, 2026-07-14.**
Full history in `docs/notes/stage-gates.md`'s status table and its "S3 first/second/third proof"
sections: exit criterion met three times over (`TimerSample`, `PerimeterSafetyAlarms` — Green-tier
reference corpus; `PlantAutoControl` — real JOB9002 content at full 20-network scale), the JOB9002
data-boundary approval explicitly extended to cover write activity first, two real converter gaps
closed (embedded-newline guard; the previously-untested "edit an existing title" scenario), one
real pre-existing corpus bug found and fixed (`TimerSample.ir`'s stale sidecar format), and the
other 13 committed reference-corpus files swept afterward to confirm that bug wasn't a wider gap.

**S4 has not been opened.** Its own entry blocker (`06-lad-conventions.md` populated) was already
cleared earlier in the project, so nothing stands in the way whenever the project owner wants to
start it — but that's a separate decision, not made yet. Do not perform S4+ capabilities
(convention review/generate/modify) until it's explicitly opened — `CLAUDE.md` hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: none — between phases

Nothing in-flight. S3 is closed; S4 is not yet open.

## Open question carried over from S1 (still needs the project owner's input, unrelated to S3)

- **`Modbus_Master`/`Modbus_Comm_Load` — built and unit-tested, live compile blocked by a
  confirmed general Openness limitation, not a converter bug.** Both instructions' own port/wire/
  parameter modeling is verified correct (every "tag not defined"/type-mismatch error cleared
  during live verification). What's left unverified is TIA accepting a standalone instance DB for
  either instruction in `SampleProject`: `create-instance-db` deterministically assigns an invalid
  `DB0` for `Modbus_Master_DB`, and neither `Modbus_Master_DB` nor `MB_Master_Comm` is exportable
  from `JOB9002` (both invisible to `SW.Blocks` entirely — same class of limitation as `WAIT`'s own
  neighbor finding, `CycleDelayReset`; full detail in `docs/notes/openness-quirks.md`). No
  Openness-exposed fix found. **Ask**: is this worth a source-side fix in `JOB9002` (the only path
  that resolved `CycleDelayReset`), or is Openness-API-only live verification for standalone
  system-FB instances simply out of reach and worth accepting as a documented, permanent
  limitation?

This is a converter/tooling-side gap, orthogonal to S3's own comment-writing work. Carried here so
it doesn't get lost, not because S3 depends on it.

**Deliberately deferred, not a bug to chase:**
- `Main` (OB1) doesn't round-trip through the full cycle — each fix reveals another narrow
  OB-specific Interface-section quirk (`SecondaryType`/`Informative` fixed; `Output` section
  validity still open). `Main` is TIA's own auto-generated template block, not restricted content,
  and OB support was never a stated project goal. Deferred per the project owner's own call.
- Modbus_Master/Modbus_Comm_Load's multi-instance form — considered during reference-corpus
  growth and deliberately not attempted (a closed engineering call, not something needing the
  project owner's input). Would have stacked two independently-unproven assumptions for one FC's
  worth of optional coverage. Revisit only if a real grounded example of Modbus multi-instance
  usage ever turns up.
- **`WAIT`/`Jump` — explicitly closed as not needed, project owner's own call, 2026-07-14.**
  Carried forward from S1's own sign-off as open questions needing input; now resolved rather than
  deferred. `WAIT` hit "An instruction with the name 'WAIT' cannot be found" against
  `SampleProject` specifically during live verification (likely a missing library/technology-object
  dependency there, never identified). `Jump` needed a real IR-format design decision — `JMP`'s
  `label` port references a new `Access Scope="Label"` node shape, and the actual jump target is a
  network-level `<Labels>` element in a *different* `CompileUnit` than the `Jump` Part itself — that
  decision was never made. Full grounding detail preserved in `ir/SPEC.md`'s own entries for each,
  in case either becomes relevant again later — not deleted, just no longer tracked as open.

## Sanitization maps built and kept

`sanitization/` (gitignored): all 8 dependency FBs' own maps, plus `PlantAutoControl.map.json` itself
and per-instance maps for all 26 DB/tag-table roots. Reusable directly for any follow-on work
against the same real project.
