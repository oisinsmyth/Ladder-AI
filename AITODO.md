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
the first pipeline artifacts live in `gen/test-project001/` (69-REQ requirements register,
architecture baseline, telemetry); test-project001's functional verdict stands at
45 implemented / 7 partial / 6 unimplemented / 10 disarmed / **0 contradicted** after fix wave 1
(2026-07-17, device compile 0/0, invariance proven, triple-reviewed). Exit criterion (ten
approved requests) not yet closed — counting rule awaits the owner (owner-questions D-4). Full
history: stage-gates entries 2026-07-16 → 2026-07-17.

## Current task: none

The 2026-07-17 owner-questions batch, the agent-tasks dispatch board it spawned, and the
GenProject1 -> test-project001 codename cleanup are all closed and documented (`docs/notes/
stage-gates.md`'s dated entries; `docs/notes/owner-questions.md` is back to empty per its own
reusable-template policy; `agent-tasks/` is back to a template folder). Nothing in-flight right
now.

**Open backlog (non-blocking):**
- Register questions with no clarification blocking them, just unanswered — `Q-02` (5 settings
  numbers), `Q-03` (local/remote selector semantics), `Q-05` (motor count), `Q-06` (start button
  action), `Q-09` (spin-down pause home), `Q-12` residual (zeroable-clock reset behavior) — all
  tracked in `gen/test-project001/requirements.md`'s Open Questions section.
- `F-3(b)` (C-003 enforcement).
- Converter reporter bug (F-1, confirmed real — C-301 count vs printed findings); converter
  capture of `HeaderAuthor`/`HeaderVersion`/`HeaderFamily` as IR header lines (would make C-201's
  author/revision mechanically checkable — still unbuilt); C-003's `iDB_<FBName>_<Instance>`
  sub-clause unenforced in `converter review`.
- Deferred items (decided in principle, timing only): `docs/notes/deferred-items.md`.

`docs/07-pattern-library-spec.md` no longer mentions a `tests/`/S9 hook — revisit if S9 ever
opens.

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
  owner's call. (Read path works: OB1 exports/converts fine, proven 2026-07-16.) **Narrowed
  2026-07-17:** this is now specifically about `Main`'s own template quirks, not "OB write support"
  generally — a hand-authored, non-`Main` OB (`OB100`, `SECONDARYTYPE Startup`) round-tripped clean
  end-to-end (startup machinery task, 2026-07-17: `converter to-xml --synthesize`,
  `openness-cli import`/`compile`, re-export readable-identical). `Main` itself stays deferred.
- Modbus multi-instance form — revisit only on a real grounded example.
- `WAIT`/`Jump` — closed as not needed (owner, 2026-07-14); grounding preserved in `ir/SPEC.md`.

## Sanitization maps built and kept

`sanitization/` (gitignored): all 8 dependency FBs' own maps, plus `PlantAutoControl.map.json` and
per-instance maps for all 26 DB/tag-table roots; `Kestrel Shredder Systems.map.json` (extended 2026-07-16
with two abbreviation pairs found during register drafting). Reusable directly for follow-on work
against the same real sources.
