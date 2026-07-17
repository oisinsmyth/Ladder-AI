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

**S4 — Convention review, DONE — gate reviewed and signed off by the project owner, 2026-07-15.**
Phase 1 (8 of ~50 rules) built, tested, live-piloted against the reference corpus, validated
against real untouched JOB9002 content. Full history: stage-gates "S4 Phase 1" and "S4:
real-content validation" sections.

**S5 — Structured data extraction, ACTIVE**, opened 2026-07-15, no work started — deprioritized
below S6/S7 per the owner (2026-07-15 "a workable S6 is the main goal"; 2026-07-17 "S7 is the
rush").

**S6 — Generation from plain language, ACTIVE**, opened 2026-07-15; the staged generation
pipeline (docs/15, ADR-0004) was adopted 2026-07-16 and largely built through 2026-07-17: all
four reviewer-adjacent skills exist and are blind-validated (`review-simplicity`,
`review-conventions`, `review-functional`, `gen-architecture`, alongside `explain-plc-block`);
the first pipeline artifacts live in `gen/GenProject1/` (69-REQ requirements register,
architecture baseline, fix-wave records, telemetry); GenProject1's functional verdict stands at
45 implemented / 7 partial / 6 unimplemented / 10 disarmed / **0 contradicted** after fix wave 1
(2026-07-17, device compile 0/0, invariance proven, triple-reviewed). Exit criterion (ten
approved requests) not yet closed — counting rule awaits the owner (owner-questions D-4). Full
history: stage-gates entries 2026-07-16 → 2026-07-17.

## Current task: AWAITING OWNER RULINGS — `docs/notes/owner-questions.md` is the single gate

State as of session close 2026-07-17: working tree clean on `master`; no worktrees or side
branches; suites green (converter 491/491, openness-cli 101/101, golden 14/14); no in-flight
code. Everything actionable is blocked on, or sequenced behind, the consolidated question list in
**`docs/notes/owner-questions.md`** (priority-ordered A–F; superseded queues formerly in this
file all point there now).

The headline decisions (section A of that doc, raised 2026-07-17):
- **A-1** gen-architecture's carving model → owner's reuse-first waterfall (library-whole →
  pattern-composed → library-modified → freeform), costed as a cheap in-passing skill edit.
- **A-2** split programming into three skills: `gen-block-new`, `gen-block-modify-purpose`,
  `gen-block-modify-fix` (+ one shared modification-choreography reference); the modify pair is
  S7's deliverable shape, sandbox-scoped until S7's gate opens.
- **A-3** bound "manual to contract": once `gen-block-new` exists, manual coding needs a per-case
  owner waiver.
- **A-4** adopt the **S7-first ordering** (owner context: library purposely thin — organic S8
  growth; S7 is the rush to take production load off the owner): (a) `converter diff` tooling
  (S7's entry requirement — formalizes the practiced readable-part invariance proofs), (b) the
  modify-skill pair exercised against the B-item defect docket, (c) D-4 ruling then close S6's
  ten requests via small deliberate asks (brings `gen-block-new` up; approvals feed S8 harvest),
  (d) A-1 rewrite in passing. Horizon: S7 on private engineering projects needs a docs/13 approval
  extension (new activity class).

On rulings received: execute per the answered items — the B-docket (six tier-1 defect candidates)
is the ready-made validation corpus for `gen-block-modify-fix`.

**Small/tooling queue (non-blocking, see owner-questions §F):** converter reporter bug (C-301
count vs printed findings); converter capture of `HeaderAuthor`/`HeaderVersion`/`HeaderFamily` as
IR header lines (would make C-201's author/revision mechanically checkable — still unbuilt);
C-003's `iDB_<FBName>_<Instance>` sub-clause unenforced in `converter review`.

**Carried forward, not urgent:** `patterns/chained-permissive-enable`'s genuinely-blind
drafted-instance gap (FI-05 / owner-questions D-5). `docs/07-pattern-library-spec.md` no longer
mentions a `tests/`/S9 hook — revisit if S9 ever opens.

## Open question carried over from S1 (still needs the project owner's input)

- **`Modbus_Master`/`Modbus_Comm_Load` — built and unit-tested, live compile blocked by a
  confirmed general Openness limitation, not a converter bug.** Both instructions' own port/wire/
  parameter modeling is verified correct. What's left unverified is TIA accepting a standalone
  instance DB for either instruction in `SampleProject`: `create-instance-db` deterministically
  assigns an invalid `DB0` for `Modbus_Master_DB`, and neither `Modbus_Master_DB` nor
  `MB_Master_Comm` is exportable from `JOB9002` (invisible to `SW.Blocks` — same class as
  `CycleDelayReset`; full detail in `docs/notes/openness-quirks.md`). **Ask**: source-side fix in
  `JOB9002` (the only path that resolved `CycleDelayReset`), or accept as a documented permanent
  limitation?

**Deliberately deferred, not a bug to chase:**
- `Main` (OB1) full round-trip (write path) — TIA's own template block; OB support never a goal;
  owner's call. (Read path works: OB1 exports/converts fine, proven 2026-07-16.)
- Modbus multi-instance form — revisit only on a real grounded example.
- `WAIT`/`Jump` — closed as not needed (owner, 2026-07-14); grounding preserved in `ir/SPEC.md`.

## Sanitization maps built and kept

`sanitization/` (gitignored): all 8 dependency FBs' own maps, plus `PlantAutoControl.map.json` and
per-instance maps for all 26 DB/tag-table roots; `Kestrel Shredder Systems.map.json` (extended 2026-07-16
with two abbreviation pairs found during register drafting). Reusable directly for follow-on work
against the same real sources.
