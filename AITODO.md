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
Phase 1 (8 of ~50 rules) built, tested (46 new tests, 412/412 suite-wide), live-piloted against all
14 reference-corpus files (matched every predicted finding exactly), then validated against real,
previously-untouched JOB9002 content (`FC StatusAlarms`, station_1). Signed off on a "shown, then
confirmed" match rather than a genuinely blind one — flagged explicitly, project owner's own
informed call to accept it. Full history: `docs/notes/stage-gates.md`'s "S4 Phase 1" and
"S4: real-content validation" sections.

**S5 — Structured data extraction, ACTIVE**, opened 2026-07-15, in parallel with S4. Per
`docs/02-roadmap.md`: extractors for alarm lists, IO usage, and cross-references, emitting
CSV/XLSX. Exit: extracted alarm and IO lists for the reference project verified against TIA's own
cross-reference data. No work started yet — deprioritized below S6-unlock work per the project
owner's own call, 2026-07-15 ("a workable S6 is the main goal").

**S6 — Generation from plain language, ACTIVE**, opened 2026-07-15. Entry criteria met: S3 done;
seed pattern library exists, redesigned around ordinary FB/FC + `CALL` (no new IR mechanism — an
earlier `template.ir`/`<SlotName>` design was built, verified sound, then abandoned when it turned
out to duplicate existing infrastructure and never reconciled with `06-lad-conventions.md` C-106).
Two pattern kinds, both seeded and ADMITTED: `patterns/motor-dol/` (all 5 criteria fully met) and
`patterns/chained-permissive-enable/` (criterion 3 accepted on partial evidence — a genuinely blind
drafted instance is still owed, recorded as an open gap, not silently closed). Full history:
`docs/notes/stage-gates.md`'s "S6 unlock" section. `docs/02-roadmap.md`'s "~10 patterns" is being
treated as an approximate target the project owner explicitly chose to satisfy with two well-proven
kinds now, growing the rest organically once S6 is actually running — not a literal blocking count.

## Current task: S6 generation-pipeline buildout (docs/15, ADR-0004) — opened 2026-07-16

Context: the first real S6 build (Kestrel Shredder → `GenProject1`, 2026-07-15, stage-gates "S6
first real proof") met every hard gate but the project owner judged the output overly complex and
obtuse. Adopted response (ADR-0004): staged skills+reviewers pipeline per
`docs/15-generation-pipeline.md`; priority order function → readability & simplicity → efficiency
now in doc 06's preamble.

- [x] Pipeline codified: `docs/15`, ADR-0004, doc 06 preamble, CLAUDE.md workflow, 00-README
      (commit `ab3efd2`)
- [x] GenProject1 exported to committed corpus: `ir/GenProject1/` + `simatic-ml/GenProject1/`,
      21 files, `.gitignore` anchored to `/GenProject1/`, data-boundary spot-check vs the
      Kestrel Shredder Systems map = 0 hits (commit `b14be52`)
- [x] Mechanical baseline (`converter review`, Debug build): 17 findings (16E/1W); both generated
      FBs clean — every in-block finding is on the real `FB_MotorFwdRevSystem`
- [x] Retrospective delivered: `docs/notes/genproject1-retrospective.md` — 12 findings, 7
      candidate C-6xx rules (§4), 6 adjudications (§5), tooling follow-ups (§6)
- [x] Owner pass on retrospective §4 + §5 (2026-07-16) — all 7 rules accepted (3 with changes),
      all 6 adjudications ruled; two interpretation points confirmed via follow-up
- [x] Folded into doc 06: new "Simplicity & readability" section (C-601–C-607), new C-203
      (titles short / comments detailed), stricter-bar principle in preamble, amendments to
      C-001 (PascalCase members) / C-109 (mapping direct-call exception) / C-122 / C-126
      (HMI-Times exception + pairing comment) / C-304 / C-307 (settings → instance UDT,
      faceplate rationale) / C-308 (one writer: the HMI; no orchestrator scan-copies);
      retrospective §10 records the resolution
- [x] `review-simplicity` built (`.claude/skills/review-simplicity/SKILL.md`, skill-creator
      guidance) and **blind-validated** against the corpus via fresh subagent — every material
      known finding reproduced + 10 new discoveries; verdict + verbatim report in
      `docs/notes/review-simplicity-validation-2026-07-16.md`. Caveat: blind executor, non-blind
      examiner; S4-style owner-independent comparison optional before gate-grade reliance.
- [x] Release converter rebuilt; `review` verb confirmed present (queued item closed)
- [x] Tier-1 rulings received and **fixed** (2026-07-16): In_Cycle = OR of the five field running
      feedbacks (owner definition: "on any time any piece of equipment is running"), computed in
      FC_ControlMain N7 — also closes the dead Infeed_Conv_Running finding; DI4 NC polarity
      negated at the input map per the pattern's negated variant. Compile clean (device 0/0),
      untouched-network invariance proven (2 hunks per block), corpus updated.
      Still open from that finding set: `RecentStart := AlwaysTrue` fighting the motor FB's own
      bit management (needs an owner ruling on intent — hand-mode constants), and
      `iDB_ShredderSequencer.IO.InCycle` member removal (deferred into the settings-rework
      request; currently unwired and documented as superseded in N7's comment).
- [ ] **OWNER: motor-dol pattern tension** — the admitted example's "HMI Times" network lacks the
      scheme comment C-126's exception now requires (and has an untitled network): update the
      pattern or waive.
- [x] Tier-1 FI adoption (2026-07-16): preflight mandatory pre-import (zero-findings bar),
      playbook first-lookup on compile failures, digest policy per stage (reviewers full-IR
      always), telemetry line per stage run — CLAUDE.md + docs/15 + docs/16 pointers; both new
      verbs verified live against the corpus first. Release converter rebuilt post-merge.
- [ ] FI-07 `openness-cli cleanup` (Portal-instance janitor) — next new build, owner go-ahead
      pending from the priority-list discussion; FI-05 (blind-draft gap) scheduled as its own
      session after
- [ ] Build `review-conventions` skill (wraps `converter review` + AI pass)
- [ ] Then per docs/15 build order: `review-functional` → `gen-architecture` → analysis skills →
      `generate` orchestrator

**Converter work queue (grew this session):** TYPE/UDT member-comment support (hard-blocked:
`UnsupportedConstructException`; C-605 error-severity is unsatisfiable on interface UDTs until
built — applies to flat, sub-struct, and separate-Settings-UDT designs alike); **member-comment
persistence verification** (fresh exports show zero member comments anywhere, including ones the
Kestrel build wrote — import→TIA→re-export survival unproven, possibly broken); Header
author/version/family capture; SPEC.md index-pairing reader note; nested-Struct-in-UDT live
round-trip (structure leg was in flight at last update — see stage-gates tail).

**Queued from the owner pass (not blocking the reviewer skills):** Struct-inside-standalone-UDT
round-trip proof (gates the grouped-UDT `Set` sub-struct design); converter capture of
`HeaderAuthor`/`HeaderVersion`/`HeaderFamily` as IR header lines (makes C-201's author/revision
mechanically checkable); `ir/SPEC.md` reader note on index-paired MUL/CONVERT rendering; two
future S6 requests on GenProject1 — settings rework (sequencer reads → UDT, delete
`FC_ControlMain`'s nine MOVEs, shrink `DB_Settings`) and `Snake_Case` buffer-member renames;
OB100/`DB_PLC` machinery whenever the demo-panel waiver (retrospective §5.5) is lifted.

**Carried forward, not urgent:** `patterns/chained-permissive-enable`'s own genuinely-blind
drafted-instance gap (see above) — worth closing before leaning on this pattern heavily, not
before S6 can start. `docs/07-pattern-library-spec.md`'s pattern anatomy no longer mentions a
`tests/`/S9 folder at all (dropped in the FB/FC redesign) — if S9 (the sim harness) ever gets
built, revisit whether patterns want a testing hook then, not a currently-open question.

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
