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

**S2 — Read and explain, ACTIVE.** S1 (lossless round-trip) formally gate-reviewed and signed off
2026-07-14 — see `docs/notes/stage-gates.md` for the full S1 history (converter/tooling now
supports ~24 instruction-level constructs; 14-block committed reference corpus; `PlantAutoControl`
itself round-trips losslessly against its full real dependency closure). This doc is wiped clean
of S1-era detail per the project owner's own instruction — that history is permanently preserved
in `docs/notes/stage-gates.md`, not lost, just no longer cluttering the in-flight scratchpad.

Per `docs/02-roadmap.md`: S2's deliverables are (a) Claude Code reading IR and producing
plain-language explanations of networks and blocks, and (b) an explanation quality checklist —
**the checklist doesn't exist yet; it's a deliverable of this stage, not a precondition for
starting it.** Exit criterion: explanations of 10 sampled networks judged accurate by the
engineer, with no hallucinated tags or behavior (design philosophy #7).

Do not perform S3+ capabilities (comment/review/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: checklist built, sampling explanations against it

The three original kickoff questions are resolved:

- **What to explain first.** Real JOB9002 blocks directly, not the sanitized reference corpus —
  explicit project-owner correction: sanitization strips exactly the real-world specificity (real
  tag names, comments, titles) that makes an explanation useful, which is the whole point of S2.
  `PerimeterSafetyAlarms`, `MotorDOL`, and `PlantAutoControl` explained and reviewed in conversation so far, all
  confirmed accurate by the project owner. (`MotorVSDSystem` was also explained but explicitly flagged as
  a demo, not counted toward the exit criterion below.)
- **What the quality checklist should check.** Built: `docs/14-s2-explanation-checklist.md`.
  Derived empirically — produced multiple independent explanations of the same real block
  (`PlantAutoControl`), varying how much project/process context each attempt got, then verified every
  claim in every attempt against source before writing down what actually separated a good
  explanation from a weaker one. Not invented solo.
- **Where explanations live.** Settled: in conversation only, per `docs/13-data-boundary.md`'s
  2026-07-14 S2-kickoff entry. Nothing about real block content gets committed; the checklist
  itself (methodology, not real project data) is the durable artifact.

**Remaining before S2's exit criterion is met:** 10 sampled network/block explanations judged
accurate by the engineer, checked against `docs/14-s2-explanation-checklist.md`. 3 counted so far
(`PerimeterSafetyAlarms`/`MotorDOL`/`PlantAutoControl`). Keep sampling and updating this count as more land.

## Open questions carried over from S1 (still need the project owner's input, unrelated to S2)

- **`WAIT` — built and unit-tested, but hit a genuinely different live blocker, still open.**
  TIA's own `Import()` rejects it — "An instruction with the name 'WAIT' cannot be found" — in
  `SampleProject` specifically, even though the regenerated XML faithfully matches the real
  `JOB9002` shape (confirmed not a converter bug: `MOVE_BLK_VARIANT`/`FillBlockI` imported into the
  same target project cleanly in the same session). Likely a library/technology-object dependency
  present in `JOB9002`'s own project but not in `SampleProject`'s — **not yet identified, needs the
  project owner's input** rather than guessed at further (adding a library/technology object to a
  project is a more consequential change than anything else done this whole plan). **Ask**: do
  you know what `WAIT` depends on / whether `SampleProject` should have it added, or would you
  rather verify `WAIT` against `JOB9002`'s own scratch copy instead (would need to extend the data
  boundary approval's scope — see `docs/13-data-boundary.md` — to cover writing new synthetic
  content into `JOB9002`, not just reading from it, which hasn't been explicitly approved yet)?
- **`Jump` was investigated and deliberately NOT built — needs the project owner's own design
  call, not a routine build.** Grounding it found `JMP` is genuine **cross-network control
  flow**: its `label` port references a new `Access Scope="Label"` node shape, and the actual
  jump target is a network-level `<Labels><LabelDeclaration></Labels>` element (a sibling of
  `<Parts>`/`<Wires>`) confirmed real in a *different* `CompileUnit` (network) than the `Jump`
  Part itself. No existing production models anything beyond its own single network — this
  needs a real IR-format decision (how should the readable form represent "network N can
  transfer control to network M"?) before any parser/reducer code gets written. Full grounding
  detail in `ir/SPEC.md`'s own `Jump` entry. **Ask the project owner how they'd like this
  represented before starting** — this is exactly the kind of design question, not
  implementation detail, that shouldn't be decided unilaterally.
- **`Modbus_Master`/`Modbus_Comm_Load` — built and unit-tested, live compile blocked by a
  confirmed general Openness limitation, not a converter bug.** Both instructions' own port/wire/
  parameter modeling is verified correct (every "tag not defined"/type-mismatch error cleared
  during live verification). What's left unverified is TIA accepting a standalone instance DB for
  either instruction in `SampleProject`: `create-instance-db` deterministically assigns an invalid
  `DB0` for `Modbus_Master_DB`, and neither `Modbus_Master_DB` nor `MB_Master_Comm` is exportable
  from `JOB9002` (both invisible to `SW.Blocks` entirely — same class of limitation as `WAIT`'s own
  neighbor finding, `CycleDelayReset`; full detail in `docs/notes/openness-quirks.md`). No
  Openness-exposed fix found. **Ask**: same question as `WAIT` in spirit — is this worth a
  source-side fix in `JOB9002` (the only path that resolved `CycleDelayReset`), or is
  Openness-API-only live verification for standalone system-FB instances simply out of reach and
  worth accepting as a documented, permanent limitation?

None of the three block S2 — they're converter/tooling-side gaps, orthogonal to reading and
explaining already-working IR. Carried here so they don't get lost, not because S2 depends on them.

**Deliberately deferred, not a bug to chase:**
- `Main` (OB1) doesn't round-trip through the full cycle — each fix reveals another narrow
  OB-specific Interface-section quirk (`SecondaryType`/`Informative` fixed; `Output` section
  validity still open). `Main` is TIA's own auto-generated template block, not restricted content,
  and OB support was never a stated project goal. Deferred per the project owner's own call.
- Modbus_Master/Modbus_Comm_Load's multi-instance form — considered during reference-corpus
  growth and deliberately not attempted (unlike the three above, a closed engineering call, not
  something needing the project owner's input). Would have stacked two independently-unproven
  assumptions for one FC's worth of optional coverage. Revisit only if a real grounded example of
  Modbus multi-instance usage ever turns up.

**Sanitization maps built and kept** (`sanitization/`, gitignored): all 8 dependency FBs' own maps,
plus `PlantAutoControl.map.json` itself and per-instance maps for all 26 DB/tag-table roots. Reusable
directly for any follow-on work against the same real project.
