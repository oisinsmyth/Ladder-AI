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

**S3 — Comment generation, ACTIVE.** S2 (read and explain) formally gate-reviewed and signed off
2026-07-14 — see `docs/notes/stage-gates.md` for the full S2 history (4 real JOB9002 blocks explained
and confirmed accurate, 51+ networks sampled well past the 10-network exit bar; explanation-quality
checklist built and committed, `docs/14-s2-explanation-checklist.md`). This doc is wiped clean of
S2-era detail per the same pattern as the S1→S2 transition — that history is permanently preserved
in `docs/notes/stage-gates.md`, not lost.

Per `docs/02-roadmap.md`: S3 is the first *write* path — comments are the lowest-risk change
because they cannot alter logic, and this is where the compile gate, import path, and
`docs/11-review-workflow.md` (explicitly agreed by the project owner, 2026-07-14, unchanged from
its original draft) actually get exercised for the first time. Deliverables: AI writes network
titles/comments and block comments into the IR; the converter carries them into SimaticML;
import+compile verified automatically. Exit: an undocumented block gets useful comments
end-to-end — generated, imported, compiled, human-approved.

Do not perform S4+ capabilities (convention review/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: S3 kickoff — no work started yet

Nothing in-flight. One real decision before writing anything, not yet made:

- **Which project S3's first proof runs against.** Two candidates, genuinely different in kind:
  `SampleProject` (the purpose-built, Green-tier reference project used throughout S1 — an
  invented, undocumented block there is a completely safe target, no data-boundary question at
  all) vs. `JOB9002`'s own scratch copy (itself already a scratch copy of the private engineering project,
  per `docs/13-data-boundary.md` — but writing *into* it, even just a comment, is a materially
  different action than the read-only export/explain work S2 did there, and hasn't been explicitly
  approved). Leaning toward `SampleProject` for the *first* proof specifically because it cleanly
  separates "does the write path work at all" from any data-boundary question — but this is worth
  confirming with the project owner, not assuming, especially since real commented blocks are
  presumably more useful to the engineer than an invented one.
- Once that's settled: pick one genuinely undocumented block/network in the chosen project, write
  a title/comment via IR, `to-xml`, `import`, `compile` — the standard S1-proven pipeline, just
  writing instead of only reading for the first time — and present the diff against
  `docs/11-review-workflow.md`'s own checklist for the engineer's approval.

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
